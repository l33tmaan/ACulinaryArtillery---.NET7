using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
//using System.Diagnostics;

namespace ACulinaryArtillery
{
    public class BlockBottleRack : Block//, IContainedMeshSource, IContainedCustomName
    {
        public MeshData GenMesh(ICoreClientAPI capi, string shapePath, ITexPositionSource texture, ITesselatorAPI tesselator = null)
        {
            var shape = capi.Assets.TryGet(shapePath + ".json").ToObject<Shape>();
            tesselator.TesselateShape(shapePath, shape, out var mesh, texture, new Vec3f(this.Shape.rotateX, this.Shape.rotateY, this.Shape.rotateZ));
            return mesh;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityBottleRack bedc)
            { bedc.OnBreak(byPlayer, pos); }
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityBottleRack bedc)
            { return bedc.OnInteract(byPlayer, blockSel); }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        /*
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (this.FirstCodePart() == "bottlerack")
            { return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode); }
            var block = world.BlockAccessor.GetBlock(blockSel.Position);
            var face = blockSel.Face.ToString();
            
            var targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
            var angle = Math.Atan2(byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X), byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z));
            angle += Math.PI;
            angle /= Math.PI / 4;

            var facing = "south";
            if (angle < 2)
            { facing = "east"; }
            else if (angle < 4)
            { facing = "north"; }
            else if (angle < 6)
            { facing = "west"; }
                
            var newPath = this.Code.Path.Replace("north", facing); 
            var blockToPlace = this.api.World.GetBlock(this.CodeWithPath(newPath));
            if (blockToPlace != null)
            {
                if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
                { return false; }
                world.BlockAccessor.SetBlock(blockToPlace.BlockId, blockSel.Position);
                return true;
            }
            return false;
        }
        */
    }
}