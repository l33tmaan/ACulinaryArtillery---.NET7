using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace ACulinaryArtillery
{
    public class BlockMixingBowl : BlockMPBase
    {
        public int CapacityLitres { get; set; }

        public int StackCapacity { get; set; }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            
            CapacityLitres = Attributes?["capacityLitres"]?.AsInt(CapacityLitres) ?? CapacityLitres;
            StackCapacity = Attributes?["stackCapacity"]?.AsInt() ?? StackCapacity;
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode))
            {
                if (!tryConnect(world, byPlayer, blockSel.Position, BlockFacing.UP))
                {
                    tryConnect(world, byPlayer, blockSel.Position, BlockFacing.DOWN);
                }

                return true;
            }

            return false;
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityMixingBowl beBowl)
            {
                if (byPlayer.Entity.Controls.Sprint && (blockSel.SelectionBoxIndex == 1 || beBowl.Inventory.openedByPlayerGUIds.Contains(byPlayer.PlayerUID)))
                {
                    beBowl.ToggleLock(byPlayer);
                    return true;
                }

                if (beBowl.CanMix && (blockSel.SelectionBoxIndex == 1 || beBowl.Inventory.openedByPlayerGUIds.Contains(byPlayer.PlayerUID)))
                {
                    beBowl.SetPlayerMixing(byPlayer, true);
                    return true;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityMixingBowl beBowl &&
                (blockSel.SelectionBoxIndex == 1 || beBowl.Inventory.openedByPlayerGUIds.Contains(byPlayer.PlayerUID)))
            {
                beBowl.SetPlayerMixing(byPlayer, true);
                return beBowl.CanMix;
            }

            return false;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            GetBlockEntity<BlockEntityMixingBowl>(blockSel.Position)?.SetPlayerMixing(byPlayer, false);
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            GetBlockEntity<BlockEntityMixingBowl>(blockSel.Position)?.SetPlayerMixing(byPlayer, false);

            return true;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return selection.SelectionBoxIndex switch
            {
                0 => [ new() {
                    ActionLangCode = "blockhelp-quern-addremoveitems",
                    MouseButton = EnumMouseButton.Right
                }, .. base.GetPlacedBlockInteractionHelp(world, selection, forPlayer)],
                _ => [ new() {
                    ActionLangCode = "aculinaryartillery:blockhelp-mixingbowl-mix",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) =>  GetBlockEntity<BlockEntityMixingBowl>(bs.Position)?.CanMix == true
                }, new() {
                    ActionLangCode = "aculinaryartillery:blockhelp-mixingbowl-autounlock",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "sprint",
                    ShouldApply = (wi, bs, es) => GetBlockEntity<BlockEntityMixingBowl>(bs.Position)?.invLocked == true
                }, new() {
                    ActionLangCode = "aculinaryartillery:blockhelp-mixingbowl-autolock",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "sprint",
                    ShouldApply = (wi, bs, es) => GetBlockEntity<BlockEntityMixingBowl>(bs.Position)?.invLocked == false
                }, .. base.GetPlacedBlockInteractionHelp(world, selection, forPlayer)]
            };
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