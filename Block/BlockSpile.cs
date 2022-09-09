using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ACulinaryArtillery
{
    public class BlockSpile : Block
    {
        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);

            if (world.BlockAccessor.GetBlock(posForward(blockPos, 2, 0, 0)) is BlockSpile) world.BlockAccessor.BreakBlock(blockPos, null);
            if (world.BlockAccessor.GetBlock(posForward(blockPos, 1, 0, 1)) is BlockSpile) world.BlockAccessor.BreakBlock(blockPos, null);
            if (world.BlockAccessor.GetBlock(posForward(blockPos, 1, 0, -1)) is BlockSpile) world.BlockAccessor.BreakBlock(blockPos, null);

        }

        public BlockPos posForward(BlockPos Pos, int offset, int height, int otheraxis)
        {
            switch (Shape.rotateY)
            {
                case 0:
                    return Pos.AddCopy(otheraxis, height, -offset);
                case 180:
                    return Pos.AddCopy(otheraxis, height, offset);
                case 90:
                    return Pos.AddCopy(-offset, height, otheraxis);
                case 270:
                    return Pos.AddCopy(offset, height, otheraxis);
            }

            return Pos;
        }
    }
}
