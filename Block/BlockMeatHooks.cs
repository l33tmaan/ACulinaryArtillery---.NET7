using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ACulinaryArtillery
{
    public class BlockMeatHooks : Block
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            // Todo: Add interaction help
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityMeatHooks behooks = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMeatHooks;
            if (behooks != null) return behooks.OnInteract(byPlayer, blockSel);


            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        /*public bool CanStay(BlockPos Pos)
        {
            if (api.World.BlockAccessor.GetBlock(Pos.UpCopy()).CanAttachBlockAt(api.World.BlockAccessor, this, Pos, BlockFacing.FromCode("up"))) return true;
            return false;
        }

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            if (CanStay(blockSel.Position)) return true;
            return false;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (!CanStay(pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
                return;
            }

            base.OnNeighbourBlockChange(world, pos, neibpos);
        }*/

    }

}
