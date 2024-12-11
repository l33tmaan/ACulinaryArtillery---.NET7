using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;

namespace ACulinaryArtillery
{

    public class BlockEntityMixingBowl : BlockEntityOpenableContainer
    {
        static SimpleParticleProperties FlourParticles;
        static SimpleParticleProperties FlourDustParticles;

        static BlockEntityMixingBowl()
        {
            // 1..20 per tick
            FlourParticles = new SimpleParticleProperties(1, 3, ColorUtil.ToRgba(40, 220, 220, 220), new Vec3d(), new Vec3d(), new Vec3f(-0.25f, -0.25f, -0.25f), new Vec3f(0.25f, 0.25f, 0.25f), 1, 1, 0.1f, 0.3f, EnumParticleModel.Quad);
            FlourParticles.AddPos.Set(1 + 2 / 32f, 0, 1 + 2 / 32f);
            FlourParticles.AddQuantity = 20;
            FlourParticles.MinVelocity.Set(-0.25f, 0, -0.25f);
            FlourParticles.AddVelocity.Set(0.5f, 1, 0.5f);
            FlourParticles.WithTerrainCollision = true;
            FlourParticles.ParticleModel = EnumParticleModel.Cube;
            FlourParticles.LifeLength = 1.5f;
            FlourParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -0.4f);

            // 1..5 per tick
            FlourDustParticles = new SimpleParticleProperties(1, 3, ColorUtil.ToRgba(40, 220, 220, 220), new Vec3d(), new Vec3d(), new Vec3f(-0.25f, -0.25f, -0.25f), new Vec3f(0.25f, 0.25f, 0.25f), 1, 1, 0.1f, 0.3f, EnumParticleModel.Quad);
            FlourDustParticles.AddPos.Set(1 + 2 / 32f, 0, 1 + 2 / 32f);
            FlourDustParticles.AddQuantity = 5;
            FlourDustParticles.MinVelocity.Set(-0.05f, 0, -0.05f);
            FlourDustParticles.AddVelocity.Set(0.1f, 0.2f, 0.1f);
            FlourDustParticles.WithTerrainCollision = false;
            FlourDustParticles.ParticleModel = EnumParticleModel.Quad;
            FlourDustParticles.LifeLength = 1.5f;
            FlourDustParticles.SelfPropelled = true;
            FlourDustParticles.GravityEffect = 0;
            FlourDustParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, 0.4f);
            FlourDustParticles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -16f);
        }


        ILoadedSound ambientSound;

        internal InventoryMixingBowl inventory;

        // For how long the current ore has been mixing
        public float inputMixTime;
        public float prevInputMixTime;
        public int CapacityLitres { get; set; }

        //For automation
        public bool invLocked;
        public ItemStack[] lockedInv = new ItemStack[6];


        GuiDialogBlockEntityMixingBowl clientDialog;
        MixingBowlTopRenderer renderer;
        bool automated;
        BEBehaviorMPConsumer mpc;


        // Server side only
        Dictionary<string, long> playersMixing = new Dictionary<string, long>();
        // Client and serverside
        int quantityPlayersMixing;

        int nowOutputFace;

        #region Getters

        public string Material
        {
            get { return Block.LastCodePart(); }
        }

        public float MixSpeed
        {
            get
            {
                if (quantityPlayersMixing > 0) return 1f;

                if (automated && mpc.Network != null) return mpc.TrueSpeed;

                return 0;
            }
        }


        MeshData mixingBowlBaseMesh
        {
            get
            {
                object value;
                Api.ObjectCache.TryGetValue(Block.FirstCodePart() + "basemesh-" + Material, out value);
                return (MeshData)value;
            }
            set { Api.ObjectCache[Block.FirstCodePart() + "basemesh-" + Material] = value; }
        }

        MeshData mixingBowlTopMesh
        {
            get
            {
                object value = null;
                Api.ObjectCache.TryGetValue(Block.FirstCodePart() + "topmesh-" + Material, out value);
                return (MeshData)value;
            }
            set { Api.ObjectCache[Block.FirstCodePart() + "topmesh-" + Material] = value; }
        }

        #endregion

        #region Config


        public virtual float maxMixingTime()
        {
            return 4;
        }

        public override string InventoryClassName
        {
            get { return Block.FirstCodePart(); }
        }

        public virtual string DialogTitle
        {
            get { return Lang.Get("aculinaryartillery:Mixing Bowl"); }
        }

        public override InventoryBase Inventory
        {
            get { return inventory; }
        }

        #endregion


        public BlockEntityMixingBowl()
        {
            inventory = new InventoryMixingBowl(null, null, this);
            inventory.SlotModified += OnSlotModifid;
        }



        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            inventory.LateInitialize(Block.FirstCodePart() + "-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);

            RegisterGameTickListener(Every100ms, 100);
            RegisterGameTickListener(Every500ms, 500);

            if (Block.Attributes["capacityLitres"].Exists == true)
            {
                CapacityLitres = Block.Attributes["capacityLitres"].AsInt(CapacityLitres);
            }

            if (ambientSound == null && api.Side == EnumAppSide.Client)
            {
                ambientSound = ((IClientWorldAccessor)api.World).LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("aculinaryartillery:sounds/block/mixing.ogg"),
                    ShouldLoop = true,
                    Position = Pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                    DisposeOnFinish = false,
                    Volume = 0.75f
                });
            }

            if (api.Side == EnumAppSide.Client)
            {
                renderer = new MixingBowlTopRenderer(api as ICoreClientAPI, Pos, GenMesh("top"));
                renderer.mechPowerPart = this.mpc;
                if (automated)
                {
                    renderer.ShouldRender = true;
                    renderer.ShouldRotateAutomated = true;
                }

                (api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, Block.FirstCodePart());

                if (mixingBowlBaseMesh == null)
                {
                    mixingBowlBaseMesh = GenMesh("base");
                }
                if (mixingBowlTopMesh == null)
                {
                    mixingBowlTopMesh = GenMesh("top");
                }
            }
        }


        public override void CreateBehaviors(Block block, IWorldAccessor worldForResolve)
        {
            base.CreateBehaviors(block, worldForResolve);

            mpc = GetBehavior<BEBehaviorMPConsumer>();
            if (mpc != null)
            {
                mpc.OnConnected = () => {
                    automated = true;
                    quantityPlayersMixing = 0;
                    if (renderer != null)
                    {
                        renderer.ShouldRender = true;
                        renderer.ShouldRotateAutomated = true;
                    }
                };

                mpc.OnDisconnected = () => {
                    automated = false;
                    if (renderer != null)
                    {
                        renderer.ShouldRender = false;
                        renderer.ShouldRotateAutomated = false;
                    }
                };
            }
        }


        public void IsMixing(IPlayer byPlayer)
        {
            SetPlayerMixing(byPlayer, true);
        }

        private void Every100ms(float dt)
        {
            float mixSpeed = MixSpeed;

            if (Api.Side == EnumAppSide.Client)
            {
                /*if (InputStack != null)
                {
                    float dustMinQ = 1 * mixSpeed;
                    float dustAddQ = 5 * mixSpeed;
                    float flourPartMinQ = 1 * mixSpeed;
                    float flourPartAddQ = 20 * mixSpeed;
                    FlourDustParticles.Color = FlourParticles.Color = InputStack.Collectible.GetRandomColor(Api as ICoreClientAPI, InputStack);
                    FlourDustParticles.Color &= 0xffffff;
                    FlourDustParticles.Color |= (200 << 24);
                    FlourDustParticles.MinQuantity = dustMinQ;
                    FlourDustParticles.AddQuantity = dustAddQ;
                    FlourDustParticles.MinPos.Set(Pos.X - 1 / 32f, Pos.Y + 11 / 16f, Pos.Z - 1 / 32f);
                    FlourDustParticles.MinVelocity.Set(-0.1f, 0, -0.1f);
                    FlourDustParticles.AddVelocity.Set(0.2f, 0.2f, 0.2f);
                    FlourParticles.MinPos.Set(Pos.X - 1 / 32f, Pos.Y + 11 / 16f, Pos.Z - 1 / 32f);
                    FlourParticles.AddQuantity = flourPartAddQ;
                    FlourParticles.MinQuantity = flourPartMinQ;
                    Api.World.SpawnParticles(FlourParticles);
                    Api.World.SpawnParticles(FlourDustParticles);
                }*/

                if (ambientSound != null && automated)
                {
                    ambientSound.SetPitch((0.5f + mpc.TrueSpeed) * 0.9f);
                    ambientSound.SetVolume(Math.Min(1f, mpc.TrueSpeed * 3f));
                }

                return;
            }


            // Only tick on the server and merely sync to client

            // Use up fuel
            if (CanMix() && mixSpeed > 0)
            {
                inputMixTime += dt * mixSpeed;

                if (inputMixTime >= maxMixingTime())
                {
                    mixInput();
                    inputMixTime = 0;
                }

                MarkDirty();
            }
        }

        private void mixInput()
        {
            CookingRecipe recipe = GetMatchingMixingRecipe(Api.World, IngredStacks);
            DoughRecipe drecipe = GetMatchingDoughRecipe(Api.World, IngredSlots);
            ItemStack mixedStack;
            int servings = 0;
            ItemStack[] stacks = IngredStacks;
            if (recipe != null)
            {
                Block cooked = Api.World.GetBlock(InputStack.Collectible.CodeWithVariant("type", "cooked"));
                mixedStack = new ItemStack(cooked);
                servings = recipe.GetQuantityServings(stacks);

                for (int i = 0; i < stacks.Length; i++)
                {
                    CookingRecipeIngredient ingred = recipe.GetIngrendientFor(stacks[i]);
                    ItemStack cookedStack = ingred.GetMatchingStack(stacks[i])?.CookedStack?.ResolvedItemstack.Clone();
                    if (cookedStack != null)
                    {
                        stacks[i] = cookedStack;
                    }
                }

                // Carry over and set perishable properties
                TransitionableProperties cookedPerishProps = recipe.PerishableProps.Clone();
                cookedPerishProps.TransitionedStack.Resolve(Api.World, "cooking container perished stack");

                CollectibleObject.CarryOverFreshness(Api, IngredSlots, stacks, cookedPerishProps);

                for (int i = 0; i < stacks.Length; i++)
                {
                    stacks[i].StackSize /= servings; // whats this good for? Probably doesn't do anything meaningful
                }


                ((BlockCookedContainer)cooked).SetContents(recipe.Code, servings, mixedStack, stacks);

                inventory[0].TakeOut(1);
                inventory[0].MarkDirty();
                for (var i = 0; i < this.IngredSlots.Length; i++)
                {
                    //the recipe must be valid at this point, so can't we just take out everything? Like so
                    if (this.IngredSlots[i].Itemstack != null)
                    {
                        this.IngredSlots[i].TakeOut(this.IngredSlots[i].Itemstack.StackSize);
                        IngredSlots[i].MarkDirty();
                    }
                }
                /*for (int i = 0; i < IngredSlots.Length; i++)
                {
                    if (IngredSlots[i].Itemstack != null)
                    {
                        if (IngredSlots[i].Itemstack.Collectible.IsLiquid())
                        { IngredSlots[i].TakeOut(servings * 10); }
                        else
                        { IngredSlots[i].TakeOut(servings); }
                        IngredSlots[i].MarkDirty();
                    }
                }*/

            }
            else if (drecipe != null)
            {
                mixedStack = drecipe.TryCraftNow(Api, IngredSlots);
            }
            else return;

            if (OutputSlot.Itemstack == null)
            {
                OutputSlot.Itemstack = mixedStack;
            }
            else
            {
                int mergableQuantity = OutputSlot.Itemstack.Collectible.GetMergableQuantity(OutputSlot.Itemstack, mixedStack, EnumMergePriority.AutoMerge);

                if (mergableQuantity > 0)
                {
                    OutputSlot.Itemstack.StackSize += mixedStack.StackSize;
                }
                else
                {
                    BlockFacing face = BlockFacing.HORIZONTALS[nowOutputFace];
                    nowOutputFace = (nowOutputFace + 1) % 4;

                    Block block = Api.World.BlockAccessor.GetBlock(this.Pos.AddCopy(face));
                    if (block.Replaceable < 6000) return;
                    Api.World.SpawnItemEntity(mixedStack, this.Pos.ToVec3d().Add(0.5 + face.Normalf.X * 0.7, 0.75, 0.5 + face.Normalf.Z * 0.7), new Vec3d(face.Normalf.X * 0.02f, 0, face.Normalf.Z * 0.02f));
                }
            }


            OutputSlot.MarkDirty();
        }


        // Sync to client every 500ms
        private void Every500ms(float dt)
        {
            if (Api.Side == EnumAppSide.Server && (MixSpeed > 0 || prevInputMixTime != inputMixTime) && inventory[0].Itemstack?.Collectible.GrindingProps != null)  //don't spam update packets when empty, as inputMixTime is irrelevant when empty
            {
                MarkDirty();
            }

            prevInputMixTime = inputMixTime;


            foreach (var val in playersMixing)
            {
                long ellapsedMs = Api.World.ElapsedMilliseconds;
                if (ellapsedMs - val.Value > 1000)
                {
                    playersMixing.Remove(val.Key);
                    break;
                }
            }
        }





        public void SetPlayerMixing(IPlayer player, bool playerMixing)
        {
            if (!automated)
            {
                if (playerMixing)
                {
                    playersMixing[player.PlayerUID] = Api.World.ElapsedMilliseconds;
                }
                else
                {
                    playersMixing.Remove(player.PlayerUID);
                }

                quantityPlayersMixing = playersMixing.Count;
            }

            updateMixingState();
        }

        bool beforeMixing;
        void updateMixingState()
        {
            if (Api?.World == null) return;

            bool nowMixing = quantityPlayersMixing > 0 || (automated && mpc.TrueSpeed > 0f);

            if (nowMixing != beforeMixing)
            {
                if (renderer != null)
                {
                    renderer.ShouldRotateManual = quantityPlayersMixing > 0;
                }

                Api.World.BlockAccessor.MarkBlockDirty(Pos, OnRetesselated);

                if (nowMixing)
                {
                    ambientSound?.Start();
                }
                else
                {
                    ambientSound?.Stop();
                }

                if (Api.Side == EnumAppSide.Server)
                {
                    MarkDirty();
                }
            }

            beforeMixing = nowMixing;
        }




        private void OnSlotModifid(int slotid)
        {
            if (Api is ICoreClientAPI)
            {
                clientDialog.Update(inputMixTime, maxMixingTime(), GetOutputText());
            }

            if (slotid == 0 || slotid > 1)
            {
                inputMixTime = 0.0f; //reset the progress to 0 if any of the input is changed.
                MarkDirty();

                if (clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.SingleComposer.ReCompose();
                }
            }
        }


        private void OnRetesselated()
        {
            if (renderer == null) return; // Maybe already disposed

            renderer.ShouldRender = quantityPlayersMixing > 0 || automated;
        }




        internal MeshData GenMesh(string type = "base")
        {
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            if (block.BlockId == 0) return null;

            MeshData mesh;
            ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;

            mesher.TesselateShape(block, Api.Assets.TryGet("aculinaryartillery:shapes/block/" + Block.FirstCodePart() + "/" + type + ".json").ToObject<Shape>(), out mesh);

            return mesh;
        }




        public bool CanMix()
        {
            return GetMatchingMixingRecipe(Api.World, IngredStacks) != null || GetMatchingDoughRecipe(Api.World, IngredSlots) != null;
        }




        #region Events

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel.SelectionBoxIndex == 1) return false;

            if (Api.World is IServerWorldAccessor)
            {
                ((ICoreServerAPI)Api).Network.SendBlockEntityPacket(
                    (IServerPlayer)byPlayer,
                    Pos,
                    (int)EnumBlockStovePacket.OpenGUI
                );

                byPlayer.InventoryManager.OpenInventory(inventory);
                MarkDirty();
            }

            return true;
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            invLocked = tree.GetBool("invLocked");
            ITreeAttribute locker = tree.GetTreeAttribute("lockedInv");
            if (locker != null)
            {
                lockedInv[0] = locker.GetItemstack("0");
                lockedInv[1] = locker.GetItemstack("1");
                lockedInv[2] = locker.GetItemstack("2");
                lockedInv[3] = locker.GetItemstack("3");
                lockedInv[4] = locker.GetItemstack("4");
                lockedInv[5] = locker.GetItemstack("5");

                for (int i = 0; i < 6; i++)
                {
                    if (lockedInv[i] != null) lockedInv[i].ResolveBlockOrItem(worldForResolving);
                }
            }

            Inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));

            if (Api != null)
            {
                Inventory.AfterBlocksLoaded(Api.World);
            }


            inputMixTime = tree.GetFloat("inputMixTime");
            nowOutputFace = tree.GetInt("nowOutputFace");

            if (worldForResolving.Side == EnumAppSide.Client)
            {
                List<int> clientIds = new List<int>((tree["clientIsMixing"] as IntArrayAttribute).value);

                quantityPlayersMixing = clientIds.Count;

                string[] playeruids = playersMixing.Keys.ToArray();

                foreach (var uid in playeruids)
                {
                    IPlayer plr = Api.World.PlayerByUid(uid);

                    if (!clientIds.Contains(plr.ClientId))
                    {
                        playersMixing.Remove(uid);
                    }
                    else
                    {
                        clientIds.Remove(plr.ClientId);
                    }
                }

                for (int i = 0; i < clientIds.Count; i++)
                {
                    IPlayer plr = worldForResolving.AllPlayers.FirstOrDefault(p => p.ClientId == clientIds[i]);
                    if (plr != null) playersMixing.Add(plr.PlayerUID, worldForResolving.ElapsedMilliseconds);
                }

                updateMixingState();
            }


            if (Api?.Side == EnumAppSide.Client && clientDialog != null)
            {
                clientDialog.Update(inputMixTime, maxMixingTime(), GetOutputText());
            }
        }



        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("invLocked", invLocked);
            ITreeAttribute locker = tree.GetOrAddTreeAttribute("lockedInv");
            locker.SetItemstack("0", lockedInv[0]);
            locker.SetItemstack("1", lockedInv[1]);
            locker.SetItemstack("2", lockedInv[2]);
            locker.SetItemstack("3", lockedInv[3]);
            locker.SetItemstack("4", lockedInv[4]);
            locker.SetItemstack("5", lockedInv[5]);

            ITreeAttribute invtree = new TreeAttribute();
            Inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;

            tree.SetFloat("inputMixTime", inputMixTime);
            tree.SetInt("nowOutputFace", nowOutputFace);
            List<int> vals = new List<int>();
            foreach (var val in playersMixing)
            {
                IPlayer plr = Api.World.PlayerByUid(val.Key);
                if (plr == null) continue;
                vals.Add(plr.ClientId);
            }


            tree["clientIsMixing"] = new IntArrayAttribute(vals.ToArray());
        }




        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (ambientSound != null)
            {
                ambientSound.Stop();
                ambientSound.Dispose();
            }

            renderer?.Dispose();
            renderer = null;
        }

        ~BlockEntityMixingBowl()
        {
            if (ambientSound != null) ambientSound.Dispose();
        }

        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid < 1000)
            {
                Inventory.InvNetworkUtil.HandleClientPacket(player, packetid, data);

                // Tell server to save this chunk to disk again
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();

                return;
            }

            if (packetid == (int)EnumBlockStovePacket.CloseGUI && player.InventoryManager != null)
            {
                player.InventoryManager.CloseInventory(Inventory);
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (packetid == (int)EnumBlockStovePacket.OpenGUI && (clientDialog == null || !clientDialog.IsOpened()))
            {
                clientDialog = new GuiDialogBlockEntityMixingBowl(DialogTitle, Inventory, Pos, Api as ICoreClientAPI);
                clientDialog.TryOpen();
                clientDialog.OnClosed += () => clientDialog = null;
                clientDialog.Update(inputMixTime, maxMixingTime(), GetOutputText());
            }

            if (packetid == (int)EnumBlockEntityPacketId.Close)
            {
                IClientWorldAccessor clientWorld = (IClientWorldAccessor)Api.World;
                clientWorld.Player.InventoryManager.CloseInventory(Inventory);
            }
        }

        #endregion

        #region Helper getters


        public BlockCookingContainer Pot
        {
            get { return inventory[0].Itemstack?.Collectible as BlockCookingContainer; }
        }

        public ItemSlot OutputSlot
        {
            get { return inventory[1]; }
        }

        public ItemStack InputStack
        {
            get { return inventory[0].Itemstack; }
            set { inventory[0].Itemstack = value; inventory[0].MarkDirty(); }
        }

        public ItemSlot[] IngredSlots
        {
            get { return new ItemSlot[] { inventory[2], inventory[3], inventory[4], inventory[5], inventory[6], inventory[7] }; }
        }

        public ItemStack[] IngredStacks
        {
            get
            {
                List<ItemStack> stacks = new List<ItemStack>(4);

                for (int i = 0; i < IngredSlots.Length; i++)
                {
                    ItemStack stack = IngredSlots[i].Itemstack;
                    if (stack == null) continue;
                    stacks.Add(stack.Clone());
                }

                return stacks.ToArray();
            }
        }

        public ItemStack OutputStack
        {
            get { return inventory[1].Itemstack; }
            set { inventory[1].Itemstack = value; inventory[1].MarkDirty(); }
        }

        #endregion


        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            foreach (var slot in Inventory)
            {
                if (slot.Itemstack == null) continue;

                if (slot.Itemstack.Class == EnumItemClass.Item)
                {
                    itemIdMapping[slot.Itemstack.Item.Id] = slot.Itemstack.Item.Code;
                }
                else
                {
                    blockIdMapping[slot.Itemstack.Block.BlockId] = slot.Itemstack.Block.Code;
                }
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            foreach (var slot in Inventory)
            {
                if (slot.Itemstack == null) continue;
                if (!slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve))
                {
                    slot.Itemstack = null;
                }
            }
        }



        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (Block == null) return false;

            mesher.AddMeshData(this.mixingBowlBaseMesh);
            if (quantityPlayersMixing == 0 && !automated)
            {
                mesher.AddMeshData(
                    this.mixingBowlTopMesh.Clone()
                    .Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, renderer.AngleRad, 0)
                    .Translate(0 / 16f, 11 / 16f, 0 / 16f)
                );
            }


            return true;
        }

        public CookingRecipe GetMatchingMixingRecipe(IWorldAccessor world, ItemStack[] stacks)
        {
            if (Pot == null) return null;
            //var recipes = MixingRecipeRegistry.Registry.MixingRecipes;
            var recipes = Api.GetMixingRecipes();
            if (recipes == null) return null;

            for (int j = 0; j < recipes.Count; j++)
            {
                if (recipes[j].Matches(stacks))
                {
                    if (recipes[j].GetQuantityServings(stacks) > (Pot?.MaxServingSize ?? 6)) continue;

                    return recipes[j];
                }
            }

            return null;
        }

        public DoughRecipe GetMatchingDoughRecipe(IWorldAccessor world, ItemSlot[] slots)
        {
            if (Pot != null) return null;
            //List<DoughRecipe> recipes = MixingRecipeRegistry.Registry.KneadingRecipes;
            var recipes = Api.GetKneadingRecipes();
            if (recipes == null) return null;

            for (int j = 0; j < recipes.Count; j++)
            {
                if (recipes[j].Matches(Api.World, slots))
                {
                    return recipes[j];
                }
            }

            return null;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Dispose();
        }

        public string GetOutputText()
        {
            CookingRecipe recipe = GetMatchingMixingRecipe(Api.World, IngredStacks);
            DoughRecipe drecipe = GetMatchingDoughRecipe(Api.World, IngredSlots);
            string locked = invLocked ? Lang.Get("aculinaryartillery:(Locked) ") : "";

            if (recipe != null)
            {
                double quantity = recipe.GetQuantityServings(IngredStacks);
                if (quantity != 1)
                {
                    return locked + Lang.Get("mealcreation-makeplural", (int)quantity, recipe.GetOutputName(Api.World, IngredStacks).ToLowerInvariant());
                }
                else
                {
                    return locked + Lang.Get("mealcreation-makesingular", (int)quantity, recipe.GetOutputName(Api.World, IngredStacks).ToLowerInvariant());
                }
            }
            else if (drecipe != null)
            {
                return locked + drecipe.GetOutputName();
            }

            return locked;

        }

        public void ToggleLock(IPlayer player = null)
        {
            Api.World.PlaySoundAt(new AssetLocation("aculinaryartillery:sounds/lock.ogg"), Pos.X, Pos.Y, Pos.Z, player);
            if (invLocked)
            {
                invLocked = false;
                return;
            }

            invLocked = true;

            for (int i = 0; i < 6; i++)
            {
                lockedInv[i] = inventory[i + 2].Itemstack?.Clone();
            }
        }

    }
}