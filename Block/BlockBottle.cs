namespace ACulinaryArtillery
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Vintagestory.API.Client;
    using Vintagestory.API.Common;
    using Vintagestory.API.Common.Entities;
    using Vintagestory.API.Config;
    using Vintagestory.API.MathTools;
    using Vintagestory.API.Server;
    using Vintagestory.API.Util;
    using Vintagestory.GameContent;
    using Vintagestory.API.Datastructures;
    using System.Diagnostics;
    using Cairo.Freetype;

    public class BlockBottle : BlockLiquidContainerBase, IContainedMeshSource, IContainedCustomName
    {
        private LiquidTopOpenContainerProps props = new();
        protected virtual string MeshRefsCacheKey => this.Code.ToShortString() + "meshRefs";
        protected virtual AssetLocation EmptyShapeLoc => this.props.EmptyShapeLoc;
        protected virtual AssetLocation ContentShapeLoc => this.props.OpaqueContentShapeLoc;
        protected virtual AssetLocation LiquidContentShapeLoc => this.props.LiquidContentShapeLoc;
        public override float TransferSizeLitres => this.props.TransferSizeLitres;
        public override float CapacityLitres => this.props.CapacityLitres;
        public override bool CanDrinkFrom => true;
        public override bool IsTopOpened => true;
        public override bool AllowHeldLiquidTransfer => true;
        protected virtual float LiquidMaxYTranslate => this.props.LiquidMaxYTranslate;
        protected virtual float LiquidYTranslatePerLitre => this.LiquidMaxYTranslate / this.CapacityLitres;

        public AssetLocation liquidFillSoundLocation => new AssetLocation("game:sounds/effect/water-fill");
        public AssetLocation liquidDrinkSoundLocation => new AssetLocation("game:sounds/player/drink1");
        public override byte[] GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
        {
            //api.Logger.Debug("Getting Light HSV for: " + stack + " | " + this.GetContent(stack) + "|" + this.GetContent(stack)?.Item?.LightHsv?.ToString());
            return this.GetContent(stack)?.Item?.LightHsv ?? base.GetLightHsv(blockAccessor, pos, stack);
        }
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (this.Attributes?["liquidContainerProps"].Exists == true)
            { this.props = this.Attributes["liquidContainerProps"].AsObject<LiquidTopOpenContainerProps>(null, this.Code.Domain); }

            if (api.Side != EnumAppSide.Client)
            { return; }
            var capi = api as ICoreClientAPI;

            this.interactions = ObjectCacheUtil.GetOrCreate(api, "bottle", () =>
            {
                var liquidContainerStacks = new List<ItemStack>();
                foreach (var obj in api.World.Collectibles)
                {
                    if (obj is ILiquidSource || obj is ILiquidSink || obj is BlockWateringCan)
                    {
                        var stacks = obj.GetHandBookStacks(capi);
                        if (stacks == null)
                        { continue; }

                        foreach (var stack in stacks)
                        {
                            stack.StackSize = 1;
                            liquidContainerStacks.Add(stack);
                        }
                    }
                }
                var lcstacks = liquidContainerStacks.ToArray();
                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-behavior-rightclickpickup",
                        MouseButton = EnumMouseButton.Right,
                        RequireFreeHand = true
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bucket-rightclick",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = lcstacks
                    }
                };
            });
        }


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (this.Code.Path.Contains("clay"))
            { return; }

            Dictionary<int, MultiTextureMeshRef> meshrefs;
            if (capi.ObjectCache.TryGetValue(this.MeshRefsCacheKey, out var obj))
            { meshrefs = obj as Dictionary<int, MultiTextureMeshRef>; }
            else
            { capi.ObjectCache[this.MeshRefsCacheKey] = meshrefs = new Dictionary<int, MultiTextureMeshRef>(); }

            var contentStack = this.GetContent(itemstack);
            if (contentStack == null)
            { return; }

            var hashcode = this.GetStackCacheHashCode(contentStack);
            if (!meshrefs.TryGetValue(hashcode, out var meshRef))
            {
                var meshdata = this.GenMesh(capi, contentStack);
                meshrefs[hashcode] = meshRef = capi.Render.UploadMultiTextureMesh(meshdata);
            }
            renderinfo.ModelRef = meshRef;
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            // This is a little odd - you have to sneak place but if there's something in a quadrant then you don't
            // Seems to be a vanilla thing (see bowl) - let's leave it as is for now
            if (byPlayer.Entity.Controls.Sneak) //sneak place only
            { return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode); }

            // Apr11 2022 - added these three lines to prevent bottle disappearing when interacting
            // with another bottle in the bottle rack - SPANG
            var targetBlock = byPlayer.Entity.World.BlockAccessor.GetBlock(blockSel.Position);
            if (targetBlock.Id == 0)
            { return false; }

            // not a fan of returning true here - if there's a problem this might be the cause
            return true;
        }


        protected int GetStackCacheHashCode(ItemStack contentStack)
        {
            var s = contentStack.StackSize + "x" + contentStack.Collectible.Code.ToShortString();
            return s.GetHashCode();
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            if (!(api is ICoreClientAPI capi))
            { return; }

            if (capi.ObjectCache.TryGetValue(this.MeshRefsCacheKey, out var obj))
            {
                var meshrefs = obj as Dictionary<int, MultiTextureMeshRef>;
                foreach (var val in meshrefs)
                { val.Value.Dispose(); }
                capi.ObjectCache.Remove(this.MeshRefsCacheKey);
            }
        }


        public MeshData GenMesh(ICoreClientAPI capi, ItemStack contentStack, BlockPos forBlockPos = null)
        {
            Shape shape = null;
            MeshData bucketmesh = null;

            var loc = this.EmptyShapeLoc;
            if (this.Code.Path.Contains("clay")) //override shape for fired clay bottle
            {
                loc = new AssetLocationAndSource("aculinaryartillery:block/bottle/bottle.json");
                var asset = capi.Assets.TryGet(loc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
                shape = asset.ToObject<Shape>();
                capi.Tesselator.TesselateShape(this, shape, out bucketmesh, new Vec3f(this.Shape.rotateX, this.Shape.rotateY, this.Shape.rotateZ));
            }
            else if (contentStack == null) //empty bottle
            {
                var asset = capi.Assets.TryGet(loc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
                shape = asset.ToObject<Shape>();
                capi.Tesselator.TesselateShape(this, shape, out bucketmesh, new Vec3f(this.Shape.rotateX, this.Shape.rotateY, this.Shape.rotateZ));
            }
            else
            {
                var props = GetContainableProps(contentStack); //bottle with liquid
                if (props is null)
                {
                    ACulinaryArtillery.logger.Error(String.Format("Bottle with Item {0} does not have waterTightProps and will not render or work correctly. This is usually caused by removing mods. If not, check with the items author.", contentStack.Item.Code.ToString()));
                }

                var contentSource = new BottleTextureSource(capi, contentStack, props?.Texture, this);

                var level = contentStack.StackSize / (props?.ItemsPerLitre ?? 1f);

                var basePath = "aculinaryartillery:shapes/block/bottle/glassbottle";
                if (level <= 0.25f && level > 0) //the > 0 because the oninteract logic below is a little bugged
                { shape = capi.Assets.TryGet(basePath + "-1.json").ToObject<Shape>(); }
                else if (level <= 0.5f)
                { shape = capi.Assets.TryGet(basePath + "-2.json").ToObject<Shape>(); }
                else if (level < 1)
                { shape = capi.Assets.TryGet(basePath + "-3.json").ToObject<Shape>(); }
                else
                { shape = capi.Assets.TryGet(basePath + ".json").ToObject<Shape>(); }

                capi.Tesselator.TesselateShape("bucket", shape, out bucketmesh, contentSource, new Vec3f(this.Shape.rotateX, this.Shape.rotateY, this.Shape.rotateZ));
                for (int i = 0; i < bucketmesh.Flags.Length; i++)
                {
                    bucketmesh.Flags[i] = bucketmesh.Flags[i] & ~(1 << 12); // Remove water waving flag
                }
                // Water flags

                if (forBlockPos != null)
                {
                    bucketmesh.CustomInts = new CustomMeshDataPartInt(bucketmesh.FlagsCount);
                    bucketmesh.CustomInts.Count = bucketmesh.FlagsCount;
                    bucketmesh.CustomInts.Values.Fill(0x4000000); // light foam only
                    bucketmesh.CustomFloats = new CustomMeshDataPartFloat(bucketmesh.FlagsCount * 2);
                    bucketmesh.CustomFloats.Count = bucketmesh.FlagsCount * 2;
                }
            }
            return bucketmesh;
        }


        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos forBlockPos = null)
        {
            var contentStack = this.GetContent(itemstack);
            return this.GenMesh(this.api as ICoreClientAPI, contentStack, forBlockPos);
        }


        public MeshData GenMeshSideways(ICoreClientAPI capi, ItemStack contentStack, BlockPos forBlockPos = null)
        {
            var asset = capi.Assets.TryGet(this.EmptyShapeLoc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
            if (asset == null)
            { return new MeshData(); }

            var shape = asset.ToObject<Shape>();
            capi.Tesselator.TesselateShape(this, shape, out var bucketmesh, new Vec3f(this.Shape.rotateX, this.Shape.rotateY, this.Shape.rotateZ));
            if (contentStack != null && (!this.Code.Path.Contains("clay")))
            {
                var props = GetContainableProps(contentStack);
                var contentSource = new BottleTextureSource(capi, contentStack, props?.Texture, this);

                var loc = props.IsOpaque ? this.ContentShapeLoc : this.LiquidContentShapeLoc;
                //now let's immediately override that loc.  I know, right?

                // unlike genmesh, were only rendering the contents at this point
                var level = contentStack.StackSize / (props?.ItemsPerLitre ?? 1f);
                var basePath = "aculinaryartillery:block/bottle/contents-";
                if (level <= 0.25f)
                { loc = new AssetLocationAndSource(basePath + "side-1"); }
                else if (level <= 0.5f)
                { loc = new AssetLocationAndSource(basePath + "side-2"); }
                else if (level < 1)
                { loc = new AssetLocationAndSource(basePath + "side-3"); }
                else
                { loc = new AssetLocationAndSource(basePath + "full"); }

                asset = capi.Assets.TryGet(loc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
                if (asset == null)
                { return bucketmesh; }

                shape = asset.ToObject<Shape>();
                capi.Tesselator.TesselateShape(this.GetType().Name, shape, out var contentMesh, contentSource, new Vec3f(this.Shape.rotateX, this.Shape.rotateY, this.Shape.rotateZ));
                for (int i = 0; i < contentMesh.Flags.Length; i++)
                {
                    contentMesh.Flags[i] = contentMesh.Flags[i] & ~(1 << 12); // Remove water waving flag
                }
                // Water flags

                if (forBlockPos != null)
                {
                    contentMesh.CustomInts = new CustomMeshDataPartInt(contentMesh.FlagsCount);
                    contentMesh.CustomInts.Count = contentMesh.FlagsCount;
                    contentMesh.CustomInts.Values.Fill(0x4000000); // light foam only
                    contentMesh.CustomFloats = new CustomMeshDataPartFloat(contentMesh.FlagsCount * 2);
                    contentMesh.CustomFloats.Count = contentMesh.FlagsCount * 2;

                    bucketmesh.CustomInts = new CustomMeshDataPartInt(bucketmesh.FlagsCount);
                    bucketmesh.CustomInts.Count = bucketmesh.FlagsCount;
                    bucketmesh.CustomInts.Values.Fill(0x4000000); // light foam only
                    bucketmesh.CustomFloats = new CustomMeshDataPartFloat(bucketmesh.FlagsCount * 2);
                    bucketmesh.CustomFloats.Count = bucketmesh.FlagsCount * 2;
                }
                bucketmesh.AddMeshData(contentMesh);

            }
            return bucketmesh;
        }


        public string GetMeshCacheKey(ItemStack itemstack)
        {
            var contentStack = this.GetContent(itemstack);
            var s = itemstack.Collectible.Code.ToShortString() + "-" + contentStack?.StackSize + "x" + contentStack?.Collectible.Code.ToShortString();
            return s;
        }


        public string GetContainedInfo(ItemSlot inSlot)           
        {
            var litres = this.GetCurrentLitres(inSlot.Itemstack);
            var contentStack = this.GetContent(inSlot.Itemstack);
            if (litres <= 0)
            { return Lang.Get("{0} (Empty)", inSlot.Itemstack.GetName()); }

            var incontainername = Lang.Get("incontainer-" + contentStack.Class.ToString().ToLowerInvariant() + "-" + contentStack.Collectible.Code.Path);
            if (litres == 1)
            { return Lang.Get("{0} ({1} litre of {2})", inSlot.Itemstack.GetName(), litres, incontainername); }

            return Lang.Get("{0} ({1} litres of {2})", inSlot.Itemstack.GetName(), litres, incontainername);
        }


        public string GetContainedName(ItemSlot inSlot, int quantity)
        { return inSlot.Itemstack.GetName(); }


        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (byEntity.Controls.Sneak)
            {
                // the base function is handling ground storable
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            //get info about target block
            Block targetBlock = null;
            BlockEntity targetBlockEntity = null;
            if (blockSel != null)
            {
                targetBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
                targetBlockEntity = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            }

            //get bottle contents
            var content = this.GetContent(itemslot.Itemstack);

            if (targetBlockEntity != null)
            {
                //are we interacting with another liquid container?
                if (targetBlockEntity is BlockEntityLiquidContainer)
                {
                    // perform the default action for a liquid container
                    // note: this crashed once when trying f click with liquid into bucket
                    // hopefully this if statement prevents that!
                    if (blockSel != null && entitySel != null)
                    {
                        base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                        return;
                    }
                }
            }
            else
            {
                if (blockSel != null)
                {
                    var waterBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position.AddCopy(blockSel.Face)).LiquidCode == "water";
                    if (waterBlock)
                    {
                        if (content == null || content.GetName() == "water")
                        {
                            // interacting with in world water
                            var dummy = this.api.World.GetItem(new AssetLocation("game:waterportion"));
                            this.TryFillFromBlock(itemslot, byEntity, blockSel.Position.AddCopy(blockSel.Face));
                            this.api.World.PlaySoundAt(this.liquidFillSoundLocation, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, null);
                            itemslot.MarkDirty();
                            handHandling = EnumHandHandling.PreventDefault;
                            return;
                        }
                    }
                }
            }

            if (content != null && byEntity.Controls.Sprint)
            {
                // dump contents on the ground when sprinting
                this.SpillContents(itemslot, byEntity, blockSel);
                handHandling = EnumHandHandling.PreventDefault;
                return;
            }

            if (this.CanDrinkFrom)
            {
                if (this.GetNutritionProperties(byEntity.World, itemslot.Itemstack, byEntity) != null)
                {
                    // drinking vanilla liquids (milk, maybe others - honey?)
                    // base.tryEatBegin(itemslot, byEntity, ref handHandling, "drink", 4);
                    // return;
                    //base.tryEatBegin(itemslot, byEntity, ref handHandling, "drink", 4);
                    base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                    //byEntity.AnimManager?.StartAnimation("eat"); //was drink, but whatevs
                    //handHandling = EnumHandHandling.PreventDefault;
                    return;
                }
            }

            if (content != null && content.Collectible.GetNutritionProperties(byEntity.World, content, byEntity) != null)
            {
                // drinking item stacks
                byEntity.World.RegisterCallback((dt) =>
                {
                    if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                    {
                        IPlayer player = null;
                        if (byEntity is EntityPlayer)
                        { player = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID); }
                    }
                }, 500);

                //not sure that this next line really does anything
                byEntity.AnimManager?.StartAnimation("drink");
                handHandling = EnumHandHandling.PreventDefault;
            }
            if (AllowHeldLiquidTransfer || CanDrinkFrom)
            {
                // Prevent placing on normal use
                handHandling = EnumHandHandling.PreventDefaultAction;
            }
        }


        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Controls.Sneak)
            { return false; } //sneak aborts
            var content = this.GetContent(slot.Itemstack);

            if (content == null)
            { return false; }

            var pos = byEntity.Pos.AheadCopy(0.4f).XYZ;
            pos.X += byEntity.LocalEyePos.X;
            pos.Y += byEntity.LocalEyePos.Y - 0.4f;
            pos.Z += byEntity.LocalEyePos.Z;
            //pos.Y += byEntity.EyeHeight - 0.4f;

            if (secondsUsed > 0.5f && (int)(30 * secondsUsed) % 7 == 1)
            {
                byEntity.World.SpawnCubeParticles(pos, content, 0.3f, 4, 0.5f, (byEntity as EntityPlayer)?.Player);
            }

            if (byEntity.World is IClientWorldAccessor)
            {
                var tf = new ModelTransform();
                tf.EnsureDefaultValues();
                tf.Origin.Set(0f, 0, 0f);

                if (secondsUsed > 0.5f)
                {
                    tf.Translation.Y = Math.Min(0.02f, GameMath.Sin(20 * secondsUsed) / 10);
                }
                tf.Translation.X -= Math.Min(1f, secondsUsed * 4 * 1.57f);
                tf.Translation.Y -= Math.Min(0.05f, secondsUsed * 2);
                tf.Rotation.X += Math.Min(30f, secondsUsed * 350);
                tf.Rotation.Y += Math.Min(80f, secondsUsed * 350);
                byEntity.Controls.UsingHeldItemTransformAfter = tf;
                return secondsUsed <= 1f;
            }
            return true;
        }
        //Should no longer be needed by overriding TryEatStop(), did not allow for collectibleBehaviors on bottle to be applied with previous implement
        /*
        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            var content = this.GetContent(slot.Itemstack);
            var nutriProps = content?.Collectible.GetNutritionProperties(byEntity.World, content, byEntity as Entity);
            var vanilla = false;
            if (this.GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity) != null)
            {
                nutriProps = this.GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity);
                vanilla = true;
            }

            if (byEntity.World is IServerWorldAccessor && nutriProps != null && secondsUsed >= 0.95f)
            {
                var dummy = new DummySlot(content);
                if (vanilla)
                {

                    var litres = this.GetCurrentLitres(slot.Itemstack);
                    var litresToDrink = litres >= 0.25f ? 0.25f : litres;

                    var state = this.UpdateAndGetTransitionState(this.api.World, slot, EnumTransitionType.Perish);
                    var spoilState = state != null ? state.TransitionLevel : 0;
                    var satLossMul = ( GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, byEntity) );
                    var healthLossMul = ( GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, byEntity) );

                    var litresMult = 1.0f;

                    if (litres == 1)
					{ litresMult = 0.25f; }

                    if (litres == 0.75)
					{ litresMult = 0.3333f; }

                    if (litres == 0.5)
					{ litresMult = 0.5f; }

                    byEntity.ReceiveSaturation(nutriProps.Satiety * litresMult * satLossMul, nutriProps.FoodCategory);
                    IPlayer player = null;
                    if (byEntity is EntityPlayer)
                    { player = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID); }

                    this.SplitStackAndPerformAction(byEntity, slot, (stack) => this.TryTakeLiquid(stack, litresToDrink)?.StackSize ?? 0);

                    var healthChange = nutriProps.Health * litresMult * healthLossMul;
                    if (nutriProps.Intoxication > 0f)
                    {
                        var intox = byEntity.WatchedAttributes.GetFloat("intoxication");
                        byEntity.WatchedAttributes.SetFloat("intoxication", Math.Min(litresToDrink, intox + (nutriProps.Intoxication * litresMult )));
                    }
                    if (healthChange != 0)
                    {
                        byEntity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Internal, Type = healthChange > 0 ? EnumDamageType.Heal : EnumDamageType.Poison }, Math.Abs(healthChange));
                    }
                    //this.SetCurrentLitres(slot.Itemstack, litres - litresToDrink);
                    //base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
                    slot.MarkDirty();
                    player.InventoryManager.BroadcastHotbarSlot();

                    if (this.GetCurrentLitres(slot.Itemstack) == 0)
                    { this.SetContent(slot.Itemstack, null); } //null it out
                    return;
                }
                else
                {
                    content.Collectible.OnHeldInteractStop(secondsUsed, dummy, byEntity, blockSel, entitySel);
                }
                this.SetContent(slot.Itemstack, dummy.StackSize > 0 ? dummy.Itemstack : null);
                //ACulinaryArtillery.logger.Debug("Is smth fucked here? heldInteractStop current content: " + this.GetContent(slot.Itemstack)?.ToString());
                slot.MarkDirty();
                (byEntity as EntityPlayer)?.Player.InventoryManager.BroadcastHotbarSlot();
            }
        }
        */
        protected override void tryEatStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity)
        {
            
            var content = this.GetContent(slot.Itemstack);
            var nutriProps = this.GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity);

            if (byEntity.World is IServerWorldAccessor && nutriProps != null && secondsUsed >= 0.95f)
            {
                var dummy = new DummySlot(content);
                var litres = this.GetCurrentLitres(slot.Itemstack);
                var litresToDrink = litres >= 0.25f ? 0.25f : litres;
                
                var state = this.UpdateAndGetTransitionState(this.api.World, slot, EnumTransitionType.Perish);
                var spoilState = state != null ? state.TransitionLevel : 0;
                var satLossMul = (GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, byEntity));
                var healthLossMul = (GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, byEntity));

                var litresMult = 1.0f;

                if (litres == 1)
                { litresMult = 0.25f; }

                if (litres == 0.75)
                { litresMult = 0.3333f; }

                if (litres == 0.5)
                { litresMult = 0.5f; }

                byEntity.ReceiveSaturation(nutriProps.Satiety * litresMult * satLossMul, nutriProps.FoodCategory);
                IPlayer player = null;
                if (byEntity is EntityPlayer)
                { player = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID); }

                this.SplitStackAndPerformAction(byEntity, slot, (stack) => this.TryTakeLiquid(stack, litresToDrink)?.StackSize ?? 0);

                var healthChange = nutriProps.Health * litresMult * healthLossMul;
                if (nutriProps.Intoxication > 0f)
                {
                    var intox = byEntity.WatchedAttributes.GetFloat("intoxication");
                    byEntity.WatchedAttributes.SetFloat("intoxication", Math.Min(litresToDrink, intox + (nutriProps.Intoxication * litresMult)));
                }
                if (healthChange != 0)
                {
                    byEntity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Internal, Type = healthChange > 0 ? EnumDamageType.Heal : EnumDamageType.Poison }, Math.Abs(healthChange));
                }
                //this.SetCurrentLitres(slot.Itemstack, litres - litresToDrink);
                //base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
                slot.MarkDirty();
                player.InventoryManager.BroadcastHotbarSlot();

                if (this.GetCurrentLitres(slot.Itemstack) == 0)
                { this.SetContent(slot.Itemstack, null); } //null it out
                return;
            }
        }
        public override float GetContainingTransitionModifierContained(IWorldAccessor world, ItemSlot inSlot, EnumTransitionType transType)
        {
            if (transType == EnumTransitionType.Perish)
            { return this.Attributes["perishRate"].AsFloat(1); }
            return this.Attributes["cureRate"].AsFloat(1);
        }


        public override float GetContainingTransitionModifierPlaced(IWorldAccessor world, BlockPos pos, EnumTransitionType transType)
        {
            if (transType == EnumTransitionType.Perish)
            { return this.Attributes["perishRate"].AsFloat(1); }
            return this.Attributes["cureRate"].AsFloat(1);
        }

        public float SatMult
        {
            get { return Attributes?["satMult"].AsFloat(1f) ?? 1f; }
        }

        public FoodNutritionProperties[] GetPropsFromArray(float[] satieties)
        {
            if (satieties == null || satieties.Length < 6)
                return null;

            List<FoodNutritionProperties> props = new List<FoodNutritionProperties>();

            if (satieties[(int)EnumNutritionMatch.Fruit] != 0)
                props.Add(new FoodNutritionProperties() { FoodCategory = EnumFoodCategory.Fruit, Satiety = satieties[(int)EnumNutritionMatch.Fruit] * SatMult });
            if (satieties[(int)EnumNutritionMatch.Grain] != 0)
                props.Add(new FoodNutritionProperties() { FoodCategory = EnumFoodCategory.Grain, Satiety = satieties[(int)EnumNutritionMatch.Grain] * SatMult });
            if (satieties[(int)EnumNutritionMatch.Vegetable] != 0)
                props.Add(new FoodNutritionProperties() { FoodCategory = EnumFoodCategory.Vegetable, Satiety = satieties[(int)EnumNutritionMatch.Vegetable] * SatMult });
            if (satieties[(int)EnumNutritionMatch.Protein] != 0)
                props.Add(new FoodNutritionProperties() { FoodCategory = EnumFoodCategory.Protein, Satiety = satieties[(int)EnumNutritionMatch.Protein] * SatMult });
            if (satieties[(int)EnumNutritionMatch.Dairy] != 0)
                props.Add(new FoodNutritionProperties() { FoodCategory = EnumFoodCategory.Dairy, Satiety = satieties[(int)EnumNutritionMatch.Dairy] * SatMult });

            if (satieties[0] != 0 && props.Count > 0)
                props[0].Health = satieties[0] * SatMult;

            return props.ToArray();
        }



        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            var content = this.GetContent(inSlot.Itemstack);

            if (content != null)
            {
                string contentPath = content.Collectible.Code.Path;
                string newDescription = content.Collectible.Code.Domain + ":itemdesc-" + contentPath;
                string finalDescription = Lang.GetMatching(newDescription);

                var dummy = new DummySlot(content);
           
                if (finalDescription != newDescription)
                {   dsc.AppendLine();
                    dsc.Append(finalDescription); }

                EntityPlayer entity = world.Side == EnumAppSide.Client ? (world as IClientWorldAccessor).Player.Entity : null;
                float spoilState = AppendPerishableInfoText(dummy, new StringBuilder(), world);

                var nutriProps = ItemExpandedRawFood.GetExpandedContentNutritionProperties(world, dummy, content, entity);
                //FoodNutritionProperties nutriProps = GetNutritionProperties(world, content, entity);


                FoodNutritionProperties[] addProps = GetPropsFromArray((content.Attributes["expandedSats"] as FloatArrayAttribute)?.value);

                if (nutriProps != null && addProps?.Length > 0)
                {
                    dsc.AppendLine();
                    dsc.AppendLine(Lang.Get("efrecipes:Extra Nutrients"));

                    foreach (FoodNutritionProperties props in addProps)
                    {
                        double liquidVolume = content.StackSize;
                        float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, content, entity);
                        float healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, content, entity);

                        if (Math.Abs(props.Health * healthLossMul) > 0.001f)
                        {
                            dsc.AppendLine(Lang.Get("efrecipes:- {0} {2} sat, {1} hp", Math.Round((props.Satiety * satLossMul) * (liquidVolume / 10 ), 1), ((props.Health * healthLossMul) * (liquidVolume / 10 )), props.FoodCategory.ToString()));
                        }
                        else
                        {
                            dsc.AppendLine(Lang.Get("efrecipes:- {0} {1} sat", Math.Round((props.Satiety * satLossMul) * (liquidVolume / 10 )), props.FoodCategory.ToString()));
                        }
                    }
                }
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!hotbarSlot.Empty && hotbarSlot.Itemstack.Collectible.Attributes?.IsTrue("handleLiquidContainerInteract") == true)
            {
                var handling = EnumHandHandling.NotHandled;
                hotbarSlot.Itemstack.Collectible.OnHeldInteractStart(hotbarSlot, byPlayer.Entity, blockSel, null, true, ref handling);

                if (handling == EnumHandHandling.PreventDefault || handling == EnumHandHandling.PreventDefaultAction)
                { return true; }
            }

            if (hotbarSlot.Empty || !(hotbarSlot.Itemstack.Collectible is ILiquidInterface))
            { return base.OnBlockInteractStart(world, byPlayer, blockSel); }

            var obj = hotbarSlot.Itemstack.Collectible;
            var singleTake = byPlayer.WorldData.EntityControls.Sneak;
            var singlePut = byPlayer.WorldData.EntityControls.Sprint;

            if (obj is ILiquidSource && !singleTake)
            {
                var moved = this.TryPutLiquid(blockSel.Position, (obj as ILiquidSource).GetContent(hotbarSlot.Itemstack), singlePut ? 1 : 9999);

                if (moved > 0)
                {
                    (obj as ILiquidSource).TryTakeContent(hotbarSlot.Itemstack, moved);
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    return true;
                }
            }

            if (obj is ILiquidSink && !singlePut)
            {
                var owncontentStack = this.GetContent(blockSel.Position);
                var moved = 0;

                if (hotbarSlot.Itemstack.StackSize == 1)
                {
                    moved = (obj as ILiquidSink).TryPutLiquid(hotbarSlot.Itemstack, owncontentStack, singleTake ? 1 : 9999);
                }
                else
                {
                    var containerStack = hotbarSlot.Itemstack.Clone();
                    containerStack.StackSize = 1;
                    moved = (obj as ILiquidSink).TryPutLiquid(containerStack, owncontentStack, singleTake ? 1 : 9999);

                    if (moved > 0)
                    {
                        hotbarSlot.TakeOut(1);
                        if (!byPlayer.InventoryManager.TryGiveItemstack(containerStack, true))
                        {
                            this.api.World.SpawnItemEntity(containerStack, byPlayer.Entity.SidedPos.XYZ);
                        }
                    }
                }

                if (moved > 0)
                {
                    this.TryTakeContent(blockSel.Position, moved);
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    return true;
                }
            }
            return true;
        }


        public override void OnGroundIdle(EntityItem entityItem)
        {
            base.OnGroundIdle(entityItem);

            if (!entityItem.Swimming || entityItem.World.Side != EnumAppSide.Server)
            { return; }

            var contents = this.GetContent(entityItem.Itemstack);
            if (contents != null && contents.Collectible.Code.Path == "rot")
            {
                entityItem.World.SpawnItemEntity(contents, entityItem.ServerPos.XYZ);
                this.SetContent(entityItem.Itemstack, null);
            }
        }


        private bool SpillContents(ItemSlot containerSlot, EntityAgent byEntity, BlockSelection blockSel)
        {
            if (blockSel == null)
            { return false; }
            var pos = blockSel.Position;
            var byPlayer = (byEntity as EntityPlayer)?.Player;
            var blockAcc = byEntity.World.BlockAccessor;
            var secondPos = blockSel.Position.AddCopy(blockSel.Face);
            var contentStack = this.GetContent(containerSlot.Itemstack);
            var props = this.GetContentProps(containerSlot.Itemstack);

            if (props == null || !props.AllowSpill || props.WhenSpilled == null)
            { return false; }

            if (!byEntity.World.Claims.TryAccess(byPlayer, secondPos, EnumBlockAccessFlags.BuildOrBreak))
            { return false; }

            var action = props.WhenSpilled.Action;
            var currentlitres = this.GetCurrentLitres(containerSlot.Itemstack);
            
            if (currentlitres > 0 && currentlitres < 10)
            { action = WaterTightContainableProps.EnumSpilledAction.DropContents; }
            //ACulinaryArtillery.logger.Debug("Action is drop contents?: " + (action == WaterTightContainableProps.EnumSpilledAction.DropContents).ToString());
            if (action == WaterTightContainableProps.EnumSpilledAction.PlaceBlock)
            {
                var waterBlock = byEntity.World.GetBlock(props.WhenSpilled.Stack.Code);
                if (props.WhenSpilled.StackByFillLevel != null)
                {
                    props.WhenSpilled.StackByFillLevel.TryGetValue((int)currentlitres, out var fillLevelStack);
                    if (fillLevelStack != null)
                    { waterBlock = byEntity.World.GetBlock(fillLevelStack.Code); }
                }

                var currentblock = blockAcc.GetBlock(pos);
                if (currentblock.Replaceable >= 6000)
                {
                    blockAcc.SetBlock(waterBlock.BlockId, pos);
                    blockAcc.TriggerNeighbourBlockUpdate(pos);
                    blockAcc.MarkBlockDirty(pos);
                }
                else
                {
                    if (blockAcc.GetBlock(secondPos).Replaceable >= 6000)
                    {
                        blockAcc.SetBlock(waterBlock.BlockId, secondPos);
                        blockAcc.TriggerNeighbourBlockUpdate(pos);
                        blockAcc.MarkBlockDirty(secondPos);
                    }
                    else
                    { return false; }
                }
            }

            if (action == WaterTightContainableProps.EnumSpilledAction.DropContents)
            {
                props.WhenSpilled.Stack.Resolve(byEntity.World, "liquidcontainerbasespill");
                var stack = props.WhenSpilled.Stack.ResolvedItemstack.Clone();
                stack.StackSize = contentStack.StackSize;
                byEntity.World.SpawnItemEntity(stack, blockSel.Position.ToVec3d().Add(blockSel.HitPosition));
            }
            var moved = this.SplitStackAndPerformAction(byEntity, containerSlot, (stack) => { this.SetContent(stack, null); return contentStack.StackSize; });
            this.DoLiquidMovedEffects(byPlayer, contentStack, moved, EnumLiquidDirection.Pour);
            return true;
        }


        private new int SplitStackAndPerformAction(Entity byEntity, ItemSlot slot, System.Func<ItemStack, int> action)
        {
            if (slot.Itemstack.StackSize == 1)
            {
                //ACulinaryArtillery.logger.Debug("slot stacksize == 1 | " + slot.Itemstack.GetName() + "contents: " + (slot.Itemstack.Collectible as BlockLiquidContainerBase).GetContent(slot.Itemstack).GetName());
                var moved = action(slot.Itemstack);
                //ACulinaryArtillery.logger.Debug("Moved = " + moved);
                if (moved > 0)
                {
                    var maxstacksize = slot.Itemstack.Collectible.MaxStackSize;

                    (byEntity as EntityPlayer)?.WalkInventory((pslot) =>
                    {
                        if (pslot.Empty || pslot is ItemSlotCreative || pslot.StackSize == pslot.Itemstack.Collectible.MaxStackSize)
                        { return true; }

                        var mergableq = slot.Itemstack.Collectible.GetMergableQuantity(slot.Itemstack, pslot.Itemstack, EnumMergePriority.DirectMerge);
                        //ACulinaryArtillery.logger.Debug("Num Mergable: " + mergableq);
                        if (mergableq == 0)
                        { return true; }

                        var selfLiqBlock = slot.Itemstack.Collectible as BlockLiquidContainerBase;
                        var invLiqBlock = pslot.Itemstack.Collectible as BlockLiquidContainerBase;
                        //ACulinaryArtillery.logger.Debug(String.Format("Self contents: {0} | Inv contents: {1}", selfLiqBlock?.GetContent(slot.Itemstack)?.ToString(), invLiqBlock?.GetContent(pslot.Itemstack)?.ToString()));
                        if ((selfLiqBlock?.GetContent(slot.Itemstack)?.StackSize ?? 0) != (invLiqBlock?.GetContent(pslot.Itemstack)?.StackSize ?? 0))
                        { return true; }
                        //ACulinaryArtillery.logger.Debug(String.Format("Slot: {0} | pslot: {1}", slot.Itemstack.ToString(), pslot.Itemstack.ToString()));
                        slot.Itemstack.StackSize += mergableq;
                        pslot.TakeOut(mergableq);
                        slot.MarkDirty();
                        pslot.MarkDirty();
                        return true;
                    });
                }
                return moved;
            }
            else
            {
                //ACulinaryArtillery.logger.Debug("slot stacksize > 1 | " + slot.Itemstack.GetName());
                var containerStack = slot.Itemstack.Clone();
                containerStack.StackSize = 1;
                var moved = action(containerStack);
                //ACulinaryArtillery.logger.Debug("Moved = " + moved);
                if (moved > 0)
                {
                    slot.TakeOut(1);
                    if ((byEntity as EntityPlayer)?.Player.InventoryManager.TryGiveItemstack(containerStack, true) != true)
                    {
                        this.api.World.SpawnItemEntity(containerStack, byEntity.SidedPos.XYZ);
                    }
                    slot.MarkDirty();
                }
                return moved;
            }
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        { return this.interactions; }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            //Debug.WriteLine("Quantity: " + this.GetCurrentLitres(inSlot.Itemstack));
            //Debug.WriteLine("Capacity: " + this.CapacityLitres);
            //Debug.WriteLine("Bottle Type: " + inSlot.Itemstack.GetName());
            //if (inSlot.Itemstack != null)
            //{
            //    var content = this.GetContent(inSlot.Itemstack);
            //    if (content != null)
            //    { Debug.WriteLine("Liquid Type: " + this.GetContent(inSlot.Itemstack).GetName()); }
            //}
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-empty",
                    HotKeyCode = "sprint",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) =>
                    {
                        return this.GetCurrentLitres(inSlot.Itemstack) > 0;
                    }
                },

                 new WorldInteraction()
                {
                    ActionLangCode = "aculinaryartillery:heldhelp-drink",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) =>
                    {
                        if (inSlot.Itemstack != null)
                        {
                            var content = this.GetContent(inSlot.Itemstack);
                            if (content != null)
                            {
                                var ltype = this.GetContent(inSlot.Itemstack).GetName();
                                if (ltype != null)
                                { return this.GetCurrentLitres(inSlot.Itemstack) > 0 && ltype != "Water"; }
                                }
                        }
                        return false;
                    }
                },
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-fill",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) =>
                    {
                        //var selblock = bs.Position.AddCopy(bs.Face).GetName();
                        if (bs != null)
                        {
                            var tBlock = this.api.World.BlockAccessor.GetBlock(bs.Position.AddCopy(bs.Face));
                            if (tBlock != null)
                            {
                                return this.GetCurrentLitres(inSlot.Itemstack) == 0 && tBlock.Code.GetName().Contains("water-");
                            }
                        }
                        return false;
                    }
                },
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-place",
                    HotKeyCode = "sneak",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => true
                }
            };
        }
    }


    /*************************************************************************************************************/
    public class BottleTextureSource : ITexPositionSource
    {
        public ItemStack forContents;
        private readonly ICoreClientAPI capi;
        private TextureAtlasPosition contentTextPos;
        private readonly TextureAtlasPosition blockTextPos;
        private readonly TextureAtlasPosition corkTextPos;
        private readonly CompositeTexture contentTexture;

        public BottleTextureSource(ICoreClientAPI capi, ItemStack forContents, CompositeTexture contentTexture, Block bottle)
        {
            this.capi = capi;
            this.forContents = forContents;
            this.contentTexture = contentTexture;
            this.corkTextPos = capi.BlockTextureAtlas.GetPosition(bottle, "map");
            this.blockTextPos = capi.BlockTextureAtlas.GetPosition(bottle, "glass");
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "map" && this.corkTextPos != null)
                { return this.corkTextPos; }
                if (textureCode == "glass" && this.blockTextPos != null)
                { return this.blockTextPos; }
                if (this.contentTextPos == null)
                {
                    int textureSubId;
                    textureSubId = ObjectCacheUtil.GetOrCreate<int>(this.capi, "contenttexture-" + this.contentTexture?.ToString() ?? "unkowncontent", () =>
                    {
                        var id = 0;
                        var bmp = this.capi.Assets.TryGet(this.contentTexture?.Base.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png") ?? new AssetLocation("aculinaryartillery:textures/block/unknown.png"))?.ToBitmap(this.capi);

                        if (bmp != null)
                        {
                            //if (contentTexture.Alpha != 255)
                            //{ bmp.MulAlpha(contentTexture.Alpha); }

                            // for now, a try catch will have to suffice - barf
                            try
                            {
                                this.capi.BlockTextureAtlas.InsertTexture(bmp, out id, out var texPos);
                            }
                            catch
                            { }
                            bmp.Dispose();
                        }
                        return id;
                    });
                    //ACulinaryArtillery.logger.Debug("Texture subId: " + textureSubId);
                    this.contentTextPos = this.capi.BlockTextureAtlas.Positions[textureSubId];
                    //ACulinaryArtillery.logger.Debug(String.Format("Unkown text pos at: {0} {1} {2} {3}", this.capi.BlockTextureAtlas.UnknownTexturePosition.x1, this.capi.BlockTextureAtlas.UnknownTexturePosition.x2, this.capi.BlockTextureAtlas.UnknownTexturePosition.y1, this.capi.BlockTextureAtlas.UnknownTexturePosition.y2) + String.Format("Created new text pos at: {0} {1} {2} {3}", this.contentTextPos.x1, this.contentTextPos.x2, this.contentTextPos.y1, this.contentTextPos.y2));
                }
                //ACulinaryArtillery.logger.Debug(String.Format("Tex pos already existed at: {0} {1} {2} {3}", this.contentTextPos.x1, this.contentTextPos.x2, this.contentTextPos.y1, this.contentTextPos.y2));
                return this.contentTextPos;
            }
        }
        public Size2i AtlasSize => this.capi.BlockTextureAtlas.Size;
    }
}
