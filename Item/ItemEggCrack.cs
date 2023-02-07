using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
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

        /// <summary>
        /// <para>
        /// Utility method to determine if and how to add a liquid to a target. 
        /// </para> 
        /// <para>
        /// Eliminates repetitions (and issues) across <see cref="OnHeldInteractStart(ItemSlot, EntityAgent, BlockSelection, EntitySelection, bool, ref EnumHandHandling)"/>
        /// &amp; <see cref="OnHeldInteractStop(float, ItemSlot, EntityAgent, BlockSelection, EntitySelection)"/>
        /// </para>
        /// </summary>
        /// <param name="block">Targeted block</param>
        /// <param name="pos">Targeted position</param>
        /// <param name="selection">Targeted block selection</param>
        /// <param name="targetStack">Stack the liquid will be added to. Will be <see langword="null" /> if the targeted container/bowl/etc is currently empty.</param>
        /// <param name="tryAddLiquidAction">
        /// <para>
        /// Callback to use to actually <em>add</em> the liquid. Will correctly switch between container &amp; ground based targets. Will mark target dirty is liquid was added.
        /// </para>
        /// <para>
        /// Callback will return <see langword="false"/> if no liquid was actually added (for example if the container was full), <see langword="true"/> otherwise.</para>
        /// </param>
        /// <returns>
        /// <see langword="true"/> if liquid can be added for the <paramref name="block"/>, <paramref name="pos"/> &amp; <paramref name="selection"/>; 
        /// <see langword="false"/> if there is no container to add liquid to (direct or as ground storage) <em>or</em> if the targeted container is full 
        /// (see <see cref="ItemHoneyComb.CanSqueezeInto(Block, BlockPos)" />).
        /// </returns>
        /// <remarks>
        /// <em>Note:</em> This implementation <em>differs</em> from the default (honeycomb) target selection. Vanilla will always pick the "first available" ground stored 
        /// container. This implementation will pick the <em>targeted</em> container on the ground, and only if that is unavailable it will pick a "first available" fallback.
        /// </remarks>
        public bool CanAddLiquid(Block block, BlockPos pos, BlockSelection selection, out ItemStack targetStack, out System.Func<ItemStack, float, bool> tryAddLiquidAction) {
            if (block is BlockLiquidContainerTopOpened blcto && (pos == null || !blcto.IsFull(pos))) {
                targetStack = blcto.GetContent(pos);
                tryAddLiquidAction = (liquid, amount) => blcto.TryPutLiquid(pos, liquid, amount) != 0;
                return true;
            }
            if (pos != null) {

                // improve on vanilla implementation: dont just pick "first available" block, go with target selection, *then* first available
                bool IsSuitableItemSlot(ItemSlot slot) {
                    return slot.Itemstack?.Block is Block slotBlock && this.CanSqueezeInto(slotBlock, null);
                }

                ItemSlot PickTargetSlot(BlockEntityGroundStorage storage) {
                    return storage.GetSlotAt(selection) is ItemSlot targetedSlot && IsSuitableItemSlot(targetedSlot)
                        ? targetedSlot                                                                                  // pick what player has targeted if suitable
                        : storage.Inventory.FirstOrDefault(IsSuitableItemSlot);                                         // otherwise pick first available
                }

                if (                    
                    this.api.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityGroundStorage beg                    // there is a ground storage
                    && PickTargetSlot(beg) is ItemSlot targetSlot                                                       // pick a target slot
                    && targetSlot.Itemstack is ItemStack squeezeStack                                                   // remember the itemstack 
                    && squeezeStack.Block is BlockLiquidContainerTopOpened groundBltco                                  // grab the 'container'
                    && !groundBltco.IsFull(targetSlot.Itemstack)                                                        // make sure it's not full
                ) {
                    targetStack = targetSlot.Itemstack?.Attributes?.GetTreeAttribute("contents")?.GetItemstack("0");
                    tryAddLiquidAction = (liquid, amount) => {
                        // note how the put liquid action uses a *different* overload than for the direct injection - see also <see cref="ItemHoneyComb.OnHeldInteractStop" />
                        bool success = groundBltco.TryPutLiquid(squeezeStack, liquid, amount) != 0;
                        // embed the dirtying into the "add liquid" action
                        if (success)
                            beg.MarkDirty(true);
                        return success;
                    };
                    return true;
                }
            }
            targetStack = null;
            tryAddLiquidAction = (_, __) => false;
            return false;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || !byEntity.Controls.Sneak) return;

            byEntity.AnimManager.StartAnimation("eggcrackstart");

            IBlockAccessor blockAccessor = byEntity.World.BlockAccessor;
            Block block = blockAccessor.GetBlock(blockSel.Position);

            bool canAddLiquid = CanAddLiquid(block, blockSel.Position, blockSel, out var targetStack, out var _);

            string eggType = slot.Itemstack.Collectible.FirstCodePart(0);      //grabs currently held item's code
            string eggVariant = slot.Itemstack.Collectible.FirstCodePart(1);   //grabs 1st variant in currently held item

            bool canCrack = (canAddLiquid, targetStack, targetStack?.Collectible?.FirstCodePart(0), targetStack?.Collectible?.FirstCodePart(1)) switch {
                (false, _, _, _)                            => false,                                                                                            // cant add liquid
                (_, null, _, _)                             => true,                                                                                             // empty target stack - can always squeeze (CanAddLiquid already checks for CanSqueeze)
                (_, _, "eggyolkfullportion", var yolk)      => byEntity.Controls.Sprint && (eggType == "egg" || eggType == "limeegg") && yolk == eggVariant,     // liquid egg in container, need to be full cracking, have right egg & matching yolks
                (_, _, "eggwhiteportion", _)                => !byEntity.Controls.Sprint && (eggType == "egg" || eggType == "limeegg"),                          // egg white in container, partial cracking and right egg
                (_, _, "eggyolkportion", var yolk)          => eggType == "eggyolk" && yolk == eggVariant,                                                       // yolks - egg variant must match
                _                                           => false
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
            IBlockAccessor blockAccessor = world.BlockAccessor;
            Block block = blockAccessor.GetBlock(blockSel.Position);
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

            bool canAddLiquid = CanAddLiquid(block, blockSel.Position, blockSel, out var _, out var tryPutLiquid);
            if (!canAddLiquid)
                return;

            (ItemStack liquid, bool giveYolk) = (byEntity.Controls.Sprint, eggType) switch {
                (true, "egg") or
                (true, "limeegg")       => (eggYolkFullStack, false),
                (false, "egg") or
                (false, "limeegg")      => (eggWhiteStack, true),
                (_, "eggyolk")          => (eggYolkStack, false),
                _                       => (null, false)
            };

            if (liquid != null && !tryPutLiquid(liquid, ContainedEggLitres)) {
                return;                
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
