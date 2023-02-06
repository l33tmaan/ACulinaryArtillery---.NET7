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

            CollectibleObject targetCollectible = api.World.BlockAccessor?.GetBlockEntity(blockSel.Position) switch {
                BlockEntityGroundStorage groundStorage => groundStorage.Inventory.FirstOrDefault(slot => !slot.Empty)?.Itemstack?.Attributes?.GetTreeAttribute("contents")?.GetItemstack("0")?.Collectible,
                BlockEntityLiquidContainer container => container.Inventory.FirstNonEmptySlot?.Itemstack.Collectible,
                _ => null
            };

            string eggType = slot.Itemstack.Collectible.FirstCodePart(0);      //grabs currently held item's code
            string eggVariant = slot.Itemstack.Collectible.FirstCodePart(1);   //grabs 1st variant in currently held item

            bool canCrack = (targetCollectible, targetCollectible?.FirstCodePart(0), targetCollectible?.FirstCodePart(1)) switch {
                (null, _, _)                            => false,                                                                                         // no collectible
                (_, null, _)                            => CanSqueezeInto(block, blockSel.Position),                                                      // collectible empty
                (_, "eggyolkfullportion", var yolk)     => byEntity.Controls.Sprint && (eggType == "egg" || eggType == "limeegg") && yolk == eggVariant,  // liquid egg in container, need to be full cracking, have right egg & matching yolks
                (_, "eggwhiteportion", _)               => !byEntity.Controls.Sprint && (eggType == "egg" || eggType == "limeegg"),                       // egg white in container, partial cracking and right egg
                (_, "eggyolkportion", var yolk)         => eggType == "eggyolk" && yolk == eggVariant,                                                    // yolk in container, need yolk of right type in hand
                _ => false
            };

            if (canCrack) {         //move to OnHeldInteractStep & play eggcrack.ogg
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


            BlockLiquidContainerTopOpened receptacle = null;
            BlockEntityGroundStorage groundStorage = null;
            if (block is BlockLiquidContainerTopOpened bltoDirect) {
                receptacle = bltoDirect;
                groundStorage = null;
            } else { 
                groundStorage = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGroundStorage;
                receptacle = groundStorage?.Inventory.FirstOrDefault(gslot => gslot.Itemstack?.Block != null && CanSqueezeInto(gslot.Itemstack.Block, null))?.Itemstack.Block as BlockLiquidContainerTopOpened;
            };

            (ItemStack liquid, bool giveYolk) = (byEntity.Controls.Sprint, eggType) switch {
                (true, "egg") or
                (true, "limeegg")   => (eggYolkFullStack, false),
                (false, "egg") or
                (false, "limeegg")  => (eggWhiteStack, true),
                (_, "eggyolk")      => (eggYolkStack, false),
                _                   => (null, false)
            };

            if (liquid != null) {
                receptacle?.TryPutLiquid(blockSel.Position, liquid, ContainedEggLitres);
                groundStorage?.MarkDirty(true);
            }

            if (api.World.Side == EnumAppSide.Client) {
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

                if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityBucket) {
                    particles.MinPos.Add(new Vec3d(-0.05, 0.5, -0.05)); //add block position
                }

                Vec3d pos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
                particles.MinPos.Add(blockSel.Position); //add block position
                particles.AddPos.Set(new Vec3d(0, 0, 0)); //add position
                world.SpawnParticles(particles);
            }

            slot.TakeOut(1);
			slot.MarkDirty();

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = world.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (giveYolk) {
                stack = new ItemStack(world.GetItem(new AssetLocation(eggYolkOutput)));
            }
            if (byPlayer?.InventoryManager.TryGiveItemstack(stack) == false) {
                byEntity.World.SpawnItemEntity(stack, byEntity.SidedPos.XYZ);
            }
        }
        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }


}
