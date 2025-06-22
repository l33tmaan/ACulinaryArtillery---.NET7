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
            return GetBlockEntity<BlockEntityMeatHooks>(blockSel.Position)?.OnInteract(byPlayer, blockSel) ??
                   base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
