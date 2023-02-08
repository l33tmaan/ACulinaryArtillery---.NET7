using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using ACulinaryArtillery.Util;

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
                        Itemstacks = stacks.ToArray(),                        
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "heldhelp-crack2",
                        HotKeyCodes = new string[] {"sneak", "sprint" },
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks.ToArray(),                        
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
        /// A callback to use to add liquid to a targeted container.
        /// </para>
        /// </summary>
        /// <remarks>Will also mark <see cref="BlockEntityGroundStorage"/> as dirty if applicable and liquid is added.</remarks>
        /// <param name="liquid">Liquid to add.</param>
        /// <param name="amount">Amount to add.</param>
        /// <returns><see langword="true" /> if any amount of liquid is added; <see langword="false"/> otherwise.</returns>
        private delegate bool TryAddLiquidHandler(ItemStack liquid, float amount);

        /// <summary>
        /// <para>
        /// Utility method to determine if &amp; how to add a liquid to a target and what is potentially already in there.
        /// </para> 
        /// <para>
        /// Eliminates repetitions (and issues) across <see cref="OnHeldInteractStart(ItemSlot, EntityAgent, BlockSelection, EntitySelection, bool, ref EnumHandHandling)"/>
        /// &amp; <see cref="OnHeldInteractStop(float, ItemSlot, EntityAgent, BlockSelection, EntitySelection)"/>
        /// </para>
        /// </summary>
        /// <param name="accessor">Block accessor to use</param>
        /// <param name="selection">Targeted block selection</param>
        /// <param name="block"><em>Optional</em> targeted block. If <see langword="null" /> is passed, will use <paramref name="accessor"/> to get the block for <paramref name="selection"/>. 
        /// If callers have already retrieved the block (or need it for later processing anyway) can be supplied to cut down on block accessor usage.</param>
        /// <param name="blockEntity"><em>Optional</em> targeted block entity. If <see langword="null" /> is passed, will use <paramref name="accessor"/> to get the block entity for <paramref name="selection"/>. 
        /// If callers have already retrieved the block entity (or need it for later processing anyway) can be supplied to cut down on block accessor usage.
        /// </param>
        /// <returns>
        /// <list type="bullet">
        /// <item><see langword="null"/> if there is no suitable container (direct or via ground storage) to add liquids to</item>
        /// <item>otherwise a tuple with members 
        /// <list type="table">
        /// <item>
        /// <term>ExistingStack</term>
        /// <description>The <see cref="ItemStack"/> currently existing inside the targeted liquid container. Will be <see langword="null"/> if the targeted container is empty.</description>
        /// </item>
        /// <item>
        /// <term>TryAddLiquid</term>
        /// <description>A callback to use to try adding liquid to the targeted container. See also <seealso cref="TryAddLiquidHandler"/></description>
        /// </item>
        /// </list>
        /// </item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <em>Note:</em> This implementation <em>differs</em> from the default (honeycomb) target selection. Vanilla will always pick the "first available" ground stored 
        /// container. This implementation will pick the <em>targeted</em> container on the ground, and only if that is unavailable it will pick a "first available" fallback.
        /// </remarks>
        private (ItemStack ExistingStack, TryAddLiquidHandler TryAddLiquid)? GetLiquidOptions(
            IBlockAccessor accessor, 
            BlockSelection selection, 
            Block block = null, 
            BlockEntity blockEntity = null) {

            BlockPos pos = selection.Position;

            return (block ?? accessor.GetBlock(pos), blockEntity ?? accessor.GetBlockEntity(pos), pos) switch {
                // direct container 
                (ILiquidSink direct, var be, _) 
                    when pos == null || !direct.IsFull(pos) 
                        => be switch {
                            BlockEntitySaucepan { isSealed: true } => null,
                            _ => (
                                ExistingStack: direct.GetContent(pos),
                                TryAddLiquid: (liquid, amount) => direct.TryPutLiquid(pos, liquid, amount) != 0
                            )
                        },
                // no position - no other options
                (_, _, null) => null,
                // we have a position, and are looking at a ground storage
                (_, BlockEntityGroundStorage groundStorage, _) 
                    when this.GetSuitableTargetSlot(groundStorage, selection) is ItemSlot groundSlot                        // we have a target slot
                        && groundSlot.Itemstack is ItemStack groundStack                                                    // (remember the itemstack) 
                        && groundStack.Block is ILiquidSink sinkInGroundStack                                               // ensure its a valid liquid sink and remember it
                        && !sinkInGroundStack.IsFull(groundStack) 
                            => sinkInGroundStack switch {                                                                   // make sure its not full
                                BlockEntitySaucepan { isSealed: false }                                                     // ensure we're not a sealed cauldron - yes right now we're not groundstorable, but maybe we will
                                or _ => (
                                    ExistingStack: groundStack?.Attributes?.GetTreeAttribute("contents")?.GetItemstack("0"),
                                    TryAddLiquid: (liquid, amount) => {
                                        // note how the put liquid action uses a *different* overload than for the direct injection - see also <see cref="ItemHoneyComb.OnHeldInteractStop" />
                                        bool success = sinkInGroundStack.TryPutLiquid(groundStack, liquid, amount) != 0;
                                        // embed the dirtying into the "add liquid" action
                                        if (success)
                                            groundStorage.MarkDirty(true);
                                        return success;
                                    }
                                )
                            },                                            
                _ => null
            };
        }
        
        /// <summary>
        /// Utility method to check if an egg type is crackable.
        /// </summary>
        // TODO: move this to json attributes on the definitions????
        public static bool IsCrackableEggType(string eggType) {
            return eggType == "egg" || eggType == "limeegg";
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || !byEntity.Controls.Sneak) return;

            byEntity.AnimManager.StartAnimation("eggcrackstart");

            IBlockAccessor blockAccessor = byEntity.World.BlockAccessor;

            var liquidOptions = GetLiquidOptions(blockAccessor, blockSel);

            string eggType = slot.Itemstack.Collectible.FirstCodePart(0);      //grabs currently held item's code
            string eggVariant = slot.Itemstack.Collectible.FirstCodePart(1);   //grabs 1st variant in currently held item

            bool canCrack = (liquidOptions, liquidOptions?.ExistingStack?.Collectible?.FirstCodePart(0), liquidOptions?.ExistingStack?.Collectible?.FirstCodePart(1)) switch {
                (null, _, _)                                => false,                                                                               // cant add liquid
                ( { ExistingStack: null}, _, _)             => true,                                                                                // empty target stack - can always squeeze (CanAddLiquid already checks for CanSqueeze)
                (_, "eggyolkfullportion", var yolk)         => byEntity.Controls.Sprint && IsCrackableEggType(eggType) && yolk == eggVariant,       // liquid egg in container, need to be full cracking, have right egg & matching yolks
                (_, "eggwhiteportion", _)                   => !byEntity.Controls.Sprint && IsCrackableEggType(eggType),                            // egg white in container, partial cracking and right egg
                (_, "eggyolkportion", var yolk)             => eggType == "eggyolk" && yolk == eggVariant,                                          // yolks - egg variant must match
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

            var liquidOptions = GetLiquidOptions(blockAccessor, blockSel, block: block);
            if (liquidOptions == null)
                return;

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

            (ItemStack liquid, bool giveYolk) = (byEntity.Controls.Sprint, eggType) switch {
                (true, var type) when IsCrackableEggType(type)  => (eggYolkFullStack, false),
                (false, var type) when IsCrackableEggType(type) => (eggWhiteStack, true),
                (_, "eggyolk")                                  => (eggYolkStack, false),
                _                                               => (null, false)
            };

            if (liquid != null && !liquidOptions.Value.TryAddLiquid(liquid, ContainedEggLitres)) {
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
