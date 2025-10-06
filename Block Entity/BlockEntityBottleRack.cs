using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    public class BlockEntityBottleRack : BlockEntityDisplay, ITexPositionSource
    {
        InventoryGeneric inventory;
        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "bottlerack";
        public override string AttributeTransformCode => "onBottlerackTransform";
        private readonly int slotCount = 16;

        public BlockEntityBottleRack()
        {
            inventory = new InventoryGeneric(slotCount, null, null);
            var meshes = new MeshData[slotCount];
        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            var playerSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (playerSlot.Empty) return TryTake(byPlayer, blockSel);
            else
            {
                var colObj = playerSlot.Itemstack.Collectible;

                BlockBottle bottle = colObj as BlockBottle;
                float fullness = bottle?.GetCurrentLitres(playerSlot.Itemstack) ?? 0;
                if (bottle?.IsTopOpened == true && fullness > 0.2f)
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "bottletoofull", Lang.Get("aculinaryartillery:bottle-toofullforrack"));
                    return false;
                }

                if (colObj.Attributes?["bottlerackable"].AsBool() == true && TryPut(playerSlot, blockSel))
                {
                    Api.World.PlaySoundAt(Block?.Sounds?.Place ?? new AssetLocation("game:sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                    return true;
                }
            }
            return false;
        }

        private bool TryPut(ItemSlot slot, BlockSelection blockSel)
        {
            var index = blockSel.SelectionBoxIndex;

            if (inventory[index].Empty && slot.TryPutInto(Api.World, inventory[index]) > 0)
            {
                updateMesh(index);
                MarkDirty(true);
                return true;
            }

            return false;
        }

        private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
        {
            var index = blockSel.SelectionBoxIndex;

            if (!inventory[index].Empty)
            {
                var stack = inventory[index].TakeOut(1);

                if (byPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    Api.World.PlaySoundAt(stack.Block?.Sounds?.Place ?? new AssetLocation("game:sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                }
                else Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));

                updateMesh(index);
                MarkDirty(true);
                return true;
            }

            return false;
        }

        protected override float[][] genTransformationMatrices()
        {
            float[][] tfMatrices = new float[slotCount][];
            bool corner = Block.FirstCodePart() == "bottlerackcorner";

            for (int slot = 0; slot < slotCount; slot++)
            {
                double col = slot % 4;
                double y = Math.Floor(slot / 4f) / 4f + 0.125f;

                (double x, double z, float rot) = (corner, col) switch
                {
                    (true, 1) => (col / 4 - 0.38, -0.22f, Block.Shape.rotateY - 45),
                    (true, 2) => (col / 4 - 0.37, -0.22f, Block.Shape.rotateY - 45),
                    (true, 3) => (col / 4 - 0.37375, -0.42f, Block.Shape.rotateY - 90),
                    (_, _) => (col / 4 - 0.37625, -0.42f, Block.Shape.rotateY)
                };
                

                tfMatrices[slot] =
                    new Matrixf()
                    .Translate(0.5f, 0, 0.5f)
                    .RotateYDeg(rot)
                    .Translate(x, y, z)
                    .RotateXDeg(90)
                    .Scale(0.99f, 0.99f, 0.99f)
                    .Translate(-0.5f, 0, -0.5f)
                    .Values
                ;
            }

            return tfMatrices;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            sb.AppendLine(Lang.Get("Suitable spot for liquid storage."));
            sb.AppendLine();

            if (forPlayer?.CurrentBlockSelection == null) return;

            var index = forPlayer.CurrentBlockSelection.SelectionBoxIndex;
            if (!inventory[index].Empty)
            {
                var slot = inventory[index];
                sb.AppendLine(slot.Itemstack?.Collectible.GetCollectibleInterface<IContainedCustomName>()?.GetContainedInfo(slot) ?? slot.GetStackName() ?? Lang.Get("unknown"));
            }
        }
    }
}