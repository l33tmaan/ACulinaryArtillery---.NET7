using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent.Mechanics;

namespace ACulinaryArtillery
{
    public class BlockMixingBowl : BlockMPBase
    {
        public int CapacityLitres { get; set; }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (Attributes?["capacityLitres"].Exists == true)
            {
                CapacityLitres = Attributes["capacityLitres"].AsInt(CapacityLitres);
            }
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            bool ok = base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);

            if (ok)
            {
                if (!tryConnect(world, byPlayer, blockSel.Position, BlockFacing.UP))
                {
                    tryConnect(world, byPlayer, blockSel.Position, BlockFacing.DOWN);
                }
            }

            return ok;
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityMixingBowl beBowl = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMixingBowl;

            if (byPlayer.Entity.Controls.Sprint && beBowl != null && (blockSel.SelectionBoxIndex == 1 || beBowl.Inventory.openedByPlayerGUIds.Contains(byPlayer.PlayerUID))) { beBowl.ToggleLock(byPlayer); return true; }

            if (beBowl != null && beBowl.CanMix() && (blockSel.SelectionBoxIndex == 1 || beBowl.Inventory.openedByPlayerGUIds.Contains(byPlayer.PlayerUID)))
            {
                beBowl.SetPlayerMixing(byPlayer, true);
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityMixingBowl beBowl = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMixingBowl;

            if (beBowl != null && (blockSel.SelectionBoxIndex == 1 || beBowl.Inventory.openedByPlayerGUIds.Contains(byPlayer.PlayerUID)))
            {
                beBowl.IsMixing(byPlayer);
                return beBowl.CanMix();
            }

            return false;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityMixingBowl beBowl = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMixingBowl;
            if (beBowl != null)
            {
                beBowl.SetPlayerMixing(byPlayer, false);
            }

        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            BlockEntityMixingBowl beBowl = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMixingBowl;
            if (beBowl != null)
            {
                beBowl.SetPlayerMixing(byPlayer, false);
            }


            return true;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            if (selection.SelectionBoxIndex == 0)
            {
                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-quern-addremoveitems",
                        MouseButton = EnumMouseButton.Right
                    }
                }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
            }
            else
            {
                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "aculinaryartillery:blockhelp-mixingbowl-mix",
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) => {
                            BlockEntityMixingBowl beBowl = world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityMixingBowl;
                            return beBowl != null && beBowl.CanMix();
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "aculinaryartillery:blockhelp-mixingbowl-autounlock",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sprint",
                        ShouldApply = (wi, bs, es) => {
                            BlockEntityMixingBowl beBowl = world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityMixingBowl;
                            return beBowl != null && beBowl.invLocked;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "aculinaryartillery:blockhelp-mixingbowl-autolock",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sprint",
                        ShouldApply = (wi, bs, es) => {
                            BlockEntityMixingBowl beBowl = world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityMixingBowl;
                            return beBowl != null && !beBowl.invLocked;
                        }
                    }
                }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
            }
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {

        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            return face == BlockFacing.UP || face == BlockFacing.DOWN;
        }
    }
}