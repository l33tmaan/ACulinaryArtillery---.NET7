using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ACulinaryArtillery
{
    public class BlockBottleRack : Block
    {
        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return GetBlockEntity<BlockEntityBottleRack>(blockSel.Position)?.OnInteract(byPlayer, blockSel) ?? base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}