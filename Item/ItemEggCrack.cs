using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    public class ItemEggCrack : ItemHoneyComb
    {
        public float ContainedEggLitres = 0.1f;

        WorldInteraction[] interactions;

        public SimpleParticleProperties particles;
        Random rand = new Random();

        public override void OnLoaded(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "eggInteractions", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();

                foreach (Block block in api.World.Blocks)
                {
                    if (block.Code == null) continue;

                    if (CanSqueezeInto(block, null))
                    {
                        stacks.Add(new ItemStack(block));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "heldhelp-crack",
                        HotKeyCode = "sneak",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks.ToArray()
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "heldhelp-crack2",
                        HotKeyCodes = new string[] {"sneak", "sprint" },
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks.ToArray()
                    }
                };
            });


        }
        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || !byEntity.Controls.Sneak) return;

            byEntity.AnimManager.StartAnimation("eggcrackstart");

            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

            bool crackDatEgg = false;

            string eggType = slot.Itemstack.Collectible.FirstCodePart(0);   //grabs currently held item's code
            string eggVariant = slot.Itemstack.Collectible.FirstCodePart(1);   //grabs 1st variant in currently held item

            var bowlCheck = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGroundStorage;
            if (bowlCheck != null)
            {
                ItemSlot bowlSourceSlot = bowlCheck.Inventory.FirstOrDefault(aslot => !aslot.Empty);
                if (bowlSourceSlot != null)
                {
                    var bowlCollectible = bowlSourceSlot.Itemstack?.Attributes?.GetTreeAttribute("contents")?.GetItemstack("0")?.Collectible;
                    var bowlSourceContents = bowlCollectible?.FirstCodePart(0); //grabs bowl liquid item's code
                    var bowlYolkContents = bowlCollectible?.FirstCodePart(1); //grabs 1st variant in bowl liquid item

                    if (bowlSourceContents == null)
                    {
                        if (CanSqueezeInto(block, blockSel.Position))               //fill empty bowl
                        { crackDatEgg = true; }
                    }

                    if (byEntity.Controls.Sprint && bowlSourceContents == "eggyolkfullportion" && (eggType == "egg" || eggType == "limeegg"))   //if sprint key is pressed & egg/limeegg in hand & eggyolkfullportion in bucket, crack
                    { crackDatEgg = true; }
                    else if (!byEntity.Controls.Sprint && bowlSourceContents == "eggwhiteportion" && (eggType == "egg" || eggType == "limeegg"))                //if egg/limeegg in hand & eggwhiteportion in bucket, crack
                    { crackDatEgg = true; }
                    else if ((bowlSourceContents == "eggyolkportion" && eggType == "eggyolk") && eggVariant == bowlYolkContents)             //if eggyolk in hand & eggyolkportion in bucket AND eggyolk variant matches bucket yolk variant, crack
                    { crackDatEgg = true; }
                }
            }

            if (crackDatEgg == false)
            {
                var bucketCheck = api.World.BlockAccessor?.GetBlockEntity(blockSel.Position) as BlockEntityLiquidContainer;
                if (bucketCheck != null)
                {
                    var bucketCollectible = bucketCheck.Inventory.FirstNonEmptySlot?.Itemstack.Collectible;
                    var bucketSourceContents = bucketCollectible?.FirstCodePart(0);             //grabs bucket liquid item's code
                    var bucketYolkContents = bucketCollectible?.FirstCodePart(1);             //grabs 1st variant in bucket liquid item's code

                    if (bucketSourceContents == null)
                    {
                        if (CanSqueezeInto(block, blockSel.Position))               //fill empty bucket
                        { crackDatEgg = true; }
                    }
                    if ((byEntity.Controls.Sprint && bucketSourceContents == "eggyolkfullportion" && (eggType == "egg" || eggType == "limeegg")) && eggVariant == bucketYolkContents)             //if holding sprint key AND eggyolk in hand & eggyolkportion in bucket AND eggyolk variant matches bucket yolk variant, crack
                    { crackDatEgg = true; }
                    else if (!byEntity.Controls.Sprint && bucketSourceContents == "eggwhiteportion" && (eggType == "egg" || eggType == "limeegg"))             //if egg/limeegg in hand & eggwhiteportion in bucket, crack
                    { crackDatEgg = true; }
                    else if ((bucketSourceContents == "eggyolkportion" && eggType == "eggyolk") && eggVariant == bucketYolkContents)             //if eggyolk in hand & eggyolkportion in bucket AND eggyolk variant matches bucket yolk variant, crack
                    { crackDatEgg = true; }
                }
            }
            if (crackDatEgg)            //move to OnHeldInteractStep & play eggcrack.ogg
            {
                handling = EnumHandHandling.PreventDefault;
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null || !byEntity.Controls.Sneak) return false;

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                tf.Translation.Set(Math.Min(0.6f, secondsUsed * 2), 0, 0); //-Math.Min(1.1f / 3, secondsUsed * 4 / 3f)
                tf.Rotation.Y = Math.Min(20, secondsUsed * 90 * 2f);

                if (secondsUsed > 0.37f)
                {
                    tf.Translation.X += (float)Math.Sin(secondsUsed / 60);
                }

                if (secondsUsed > 0.4f)
                {
                    tf.Translation.X += (float)Math.Sin(Math.Min(1.0, secondsUsed) * 5) * 0.75f;
                }

                if (secondsUsed > 0.49f)
                {
                    byEntity.AnimManager.StopAnimation("eggcrackstart");
                }

                byEntity.Controls.UsingHeldItemTransformBefore = tf;
            }

            return secondsUsed < 0.5f;
        }
        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.AnimManager.StopAnimation("eggcrackstart");

            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return;
            if (secondsUsed < 0.48f) return;

            byEntity.AnimManager.StopAnimation("eggcrackstart");

            IWorldAccessor world = byEntity.World;

            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            if (!CanSqueezeInto(block, blockSel.Position)) return;

            string eggType = slot.Itemstack.Collectible.FirstCodePart(0);   //grabs currently held item's code
            string eggVariant = slot.Itemstack.Collectible.FirstCodePart(1);   //grabs 1st variant in currently held item
            string eggWhiteLiquidAsset = "aculinaryartillery:eggwhiteportion";             //default liquid output
            string eggYolkOutput = "aculinaryartillery:eggyolk-" + eggVariant;       //searches for eggVariant and adds to eggyolk item
            string eggYolkLiquidAsset = "aculinaryartillery:eggyolkportion-" + eggVariant; //searches for eggVariant and adds to eggyolkportion item
            string eggYolkFullLiquidAsset = "aculinaryartillery:eggyolkfullportion-" + eggVariant; //searches for eggVariant and adds to eggyolkfullportion item
            string eggShellOutput = "aculinaryartillery:eggshell";                    //default item output

            ItemStack eggWhiteStack = new ItemStack(world.GetItem(new AssetLocation(eggWhiteLiquidAsset)), 99999);
            ItemStack eggYolkStack = new ItemStack(world.GetItem(new AssetLocation(eggYolkLiquidAsset)), 99999);
            ItemStack eggYolkFullStack = new ItemStack(world.GetItem(new AssetLocation(eggYolkFullLiquidAsset)), 99999);
            ItemStack stack = new ItemStack(world.GetItem(new AssetLocation(eggShellOutput)));

            BlockLiquidContainerTopOpened blockCnt = block as BlockLiquidContainerTopOpened;
            BlockEntityBucket blockInventory = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBucket;
            if (blockCnt != null || blockInventory != null)
            {
                if (byEntity.Controls.Sprint && (eggType == "egg" || eggType == "limeegg"))
                {
                    blockCnt.TryPutLiquid(blockSel.Position, eggYolkFullStack, ContainedEggLitres);
                }
                else if (eggType == "egg" || eggType == "limeegg")
                {
                    blockCnt.TryPutLiquid(blockSel.Position, eggWhiteStack, ContainedEggLitres);
                }
                else if (eggType == "eggyolk")
                {
                    blockCnt.TryPutLiquid(blockSel.Position, eggYolkStack, ContainedEggLitres);
                }
                // if (blockCnt.TryPutLiquid(blockSel.Position, eggYolkStack, ContainedEggLitres) == 0) return;
            }
            else
            {
                var beg = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGroundStorage;
                if (beg != null)
                {
                    ItemSlot sourceSlot = beg.Inventory.FirstOrDefault(aslot => !aslot.Empty);
                    var sourceContents = sourceSlot.Itemstack?.Attributes?.GetTreeAttribute("contents")?.GetItemstack("0");

                    /* if (sourceContents != null)
                    {
                        //there's already something in the BOWL
                        Debug.WriteLine(sourceContents.Collectible.Code.Path); //whats in the bowl?  eggwhite perhaps?
                        Debug.WriteLine(sourceContents.StackSize); //how much stuff exactly is in the bowl?
                    } */
                    ItemSlot squeezeIntoSlot = beg.Inventory.FirstOrDefault(gslot => gslot.Itemstack?.Block != null && CanSqueezeInto(gslot.Itemstack.Block, null));
                    string containerItemPath = squeezeIntoSlot.Itemstack.Collectible.Code.Path;     //path of the container I'm looking at
                    if (squeezeIntoSlot != null)
                    {
                        blockCnt = squeezeIntoSlot.Itemstack.Block as BlockLiquidContainerTopOpened;
                        if (byEntity.Controls.Sprint && (eggType == "egg" || eggType == "limeegg"))
                        {
                            blockCnt.TryPutLiquid(squeezeIntoSlot.Itemstack, eggYolkFullStack, ContainedEggLitres);
                        }
                        else if (eggType == "egg" || eggType == "limeegg")
                        {
                            blockCnt.TryPutLiquid(squeezeIntoSlot.Itemstack, eggWhiteStack, ContainedEggLitres);
                        }
                        else if (eggType == "eggyolk")
                        {
                            blockCnt.TryPutLiquid(squeezeIntoSlot.Itemstack, eggYolkStack, ContainedEggLitres);
                        }
                        beg.MarkDirty(true);
                    }
                }
            }

            if (api.World.Side == EnumAppSide.Client)
            {
                byEntity.World.PlaySoundAt(new AssetLocation("aculinaryartillery:sounds/player/eggcrack"), byEntity, null, true, 16, 0.5f);

                // Primary Particles
                var color = ColorUtil.ToRgba(255, 219, 206, 164);

                particles = new SimpleParticleProperties(
                    4, 6, // quantity
                    color,
                    new Vec3d(0.35, 0.1, 0.35), //min position
                    new Vec3d(), //add position - see below
                    new Vec3f(0.2f, 0.5f, 0.2f), //min velocity
                    new Vec3f(), //add velocity - see below
                    (float)((rand.NextDouble() * 1f) + 0.25f), //life length
                    (float)((rand.NextDouble() * 0.05f) + 0.2f), //gravity effect 
                    0.25f, 0.5f, //size
                    EnumParticleModel.Cube // model
                    );

                particles.AddVelocity.Set(new Vec3f(-0.4f, 0.5f, -0.4f)); //add velocity
                particles.SelfPropelled = true;

                if (blockInventory != null)
                {
                    particles.MinPos.Add(new Vec3d(-0.05, 0.5, -0.05)); //add block position
                }

                Vec3d pos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
                particles.MinPos.Add(blockSel.Position); //add block position
                particles.AddPos.Set(new Vec3d(0, 0, 0)); //add position
                world.SpawnParticles(particles);
            }

            slot.TakeOut(1);
			slot.MarkDirty();

			var bowlCheck = api.World.BlockAccessor?.GetBlockEntity(blockSel.Position) as BlockEntityGroundStorage;
			CollectibleObject bowlCollectible = null;
            string bowlSourceContents = null;
			string bowlYolkContents = null;
            if (bowlCheck != null)
			{
                ItemSlot bowlSourceSlot = bowlCheck.Inventory?.FirstOrDefault(aslot => !aslot.Empty);
			    bowlCollectible = bowlSourceSlot.Itemstack?.Attributes?.GetTreeAttribute("contents")?.GetItemstack("0")?.Collectible;
				bowlSourceContents = bowlCollectible?.FirstCodePart(0); //grabs bowl liquid item's code
                bowlYolkContents = bowlCollectible?.FirstCodePart(1); //grabs 1st variant in bowl liquid item
			}

			var bucketCheck = api.World.BlockAccessor?.GetBlockEntity(blockSel.Position) as BlockEntityLiquidContainer;
			CollectibleObject bucketCollectible = null;
            string bucketSourceContents = null;
            string bucketYolkContents = null;
            if (bucketCheck != null) 
            {
                ItemSlot bucketSourceSlot = bucketCheck.Inventory?.FirstOrDefault(aslot => !aslot.Empty);
                bucketCollectible = bucketSourceSlot.Itemstack?.Attributes?.GetTreeAttribute("contents")?.GetItemstack("0")?.Collectible;
                bucketSourceContents = bucketCollectible?.FirstCodePart(0); //grabs bowl liquid item's code
                bucketYolkContents = bucketCollectible?.FirstCodePart(1); //grabs 1st variant in bowl liquid item
            }

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = world.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if ((eggType == "egg" || eggType == "limeegg") && ( bowlSourceContents == "eggwhiteportion"  || bucketSourceContents == "eggwhiteportion" ) )
            {
            stack = new ItemStack(world.GetItem(new AssetLocation(eggYolkOutput)));
            }
            if (byPlayer?.InventoryManager.TryGiveItemstack(stack) == false)
            {
            byEntity.World.SpawnItemEntity(stack, byEntity.SidedPos.XYZ);
            }
        }
        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }


}