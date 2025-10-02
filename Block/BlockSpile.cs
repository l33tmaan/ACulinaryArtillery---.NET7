using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ACulinaryArtillery
{
    public class BlockSpile : Block
    {
        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            if (blockSel.Face.IsHorizontal && hasSpile(world.BlockAccessor, blockSel.Position.Copy(), blockSel.Face))
            {
                failureCode = "alreadyhasspile";
                return false;
            }

            return base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode);
        }

        bool hasSpile(IBlockAccessor blockAccess, BlockPos pos, BlockFacing face)
        {
            pos.Add(face, 2); // check the opposite side of the log
            if (blockAccess.GetBlock(pos) is BlockSpile) return true;

            pos.Add(face, -1); // move back into the log
            face = face.GetCW(); // turn 90° clockwise
            pos.Add(face); // move to the next side
            if (blockAccess.GetBlock(pos) is BlockSpile) return true;

            pos.Add(face, -2); // and finally move back through the log to the third side
            if (blockAccess.GetBlock(pos) is BlockSpile) return true;

            return false;
        }
    }
}