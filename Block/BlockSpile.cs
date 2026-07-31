using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ACulinaryArtillery
{
    public class BlockSpile : Block
    {
        // Using CanPlaceBlock causes the error message to be overriden by BlockBehaviorHorizontalAttachable
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            Block? attachingTo = world.BlockAccessor.GetBlock(blockSel.Position.AddCopy(blockSel.Face, -1));
            if (blockSel.Face.IsHorizontal && attachingTo?.Attributes?["sapProperties"]?.AsObject<SapProperties>() == null)
            {
                failureCode = "notspileable";
                return false;
            }

            if (blockSel.Face.IsHorizontal && HasSpile(world.BlockAccessor, blockSel.Position.Copy(), blockSel.Face))
            {
                failureCode = "alreadyhasspile";
                return false;
            }

            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        }

        bool HasSpile(IBlockAccessor blockAccess, BlockPos pos, BlockFacing face)
        {
            pos.Add(face, -2); // check the opposite side of the log
            if (blockAccess.GetBlock(pos) is BlockSpile) return true;

            pos.Add(face, 1); // move back into the log
            face = face.GetCW(); // turn 90° clockwise
            pos.Add(face, -1); // move to the next side
            if (blockAccess.GetBlock(pos) is BlockSpile) return true;

            pos.Add(face, 2); // and finally move back through the log to the third side
            if (blockAccess.GetBlock(pos) is BlockSpile) return true;

            return false;
        }
    }
}
