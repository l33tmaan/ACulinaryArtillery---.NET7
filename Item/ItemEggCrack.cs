using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;

using ACulinaryArtillery.Util;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
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
            if (api.Side == EnumAppSide.Server)
            {
                JsonObject jsonLitres = Attributes?["containedEggLitres"];
                if (jsonLitres?.Exists == true)
                {
                    ContainedEggLitres = jsonLitres.AsFloat();
                }
                return;
            }

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

            var liquidOptions = this.GetLiquidOptions(blockAccessor, blockSel);

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
            else
            {
                //ACulinaryArtillery.logger.Debug("Cant crack: " + slot.Itemstack.ToString());
                //EnumHandling passThroughHandling = EnumHandling.PreventDefault;
                handling = EnumHandHandling.PreventDefault;
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                //slot.Itemstack.Collectible.GetBehavior<CollectibleBehaviorGroundStorable>().OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling, ref passThroughHandling);
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

            var liquidOptions = this.GetLiquidOptions(blockAccessor, blockSel, block: block);
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
                    // spawn particles above ellipses covering top plane of target block collision box:
                    //  - at the edge, on a line facing the aiming point
                    liquidOptions.Value.TargetBlock
                        .GetCollisionBoxes(blockAccessor, null)
                        .OrderByDescending(cf => cf.MaxY)
                        .FirstOrDefault() switch {
                            null => new Vec3d(0.35, 0.1, 0.35),
                            var b => b.TopFaceEllipsesLineIntersection(blockSel.HitPosition.ToVec3f())
                        }, 
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

                Vec3d pos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);

                particles.MinPos.Add(blockSel.Position);                            // add selection position
                particles.MinPos.Add(liquidOptions.Value.TargetBlock.TopMiddlePos); // add sub block selection position
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
