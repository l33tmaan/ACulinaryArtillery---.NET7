using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{

    public class BlockEntityBottle : BlockEntityContainer
    {
        public override InventoryBase Inventory => this.inv;
        private readonly InventoryGeneric inv;
        public override string InventoryClassName => "bottle";
        public BlockEntityBottle()
        { this.inv = new InventoryGeneric(1, null, null); }

        private BlockBottle ownBlock;
        private MeshData currentMesh;


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            this.ownBlock = this.Block as BlockBottle;

            if (this.Api.Side == EnumAppSide.Client)
            {
                this.currentMesh = this.GenMesh();
                this.MarkDirty(true);
            }

            Inventory.OnAcquireTransitionSpeed += Inventory_OnAcquireTransitionSpeed1;
        }

        private float Inventory_OnAcquireTransitionSpeed1(EnumTransitionType transType, ItemStack stack, float mulByConfig)
        {
            var mul = Inventory.GetTransitionSpeedMul(transType, stack);
            mul *= this.ownBlock?.GetContainingTransitionModifierPlaced(this.Api.World, this.Pos, transType) ?? 1;
            return mul;
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (this.Api.Side == EnumAppSide.Client)
            {
                this.currentMesh = this.GenMesh();
                this.MarkDirty(true);
            }
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (this.Api?.Side == EnumAppSide.Client)
            {
                this.currentMesh = this.GenMesh();
                this.MarkDirty(true);
            }
        }


        internal MeshData GenMesh()
        {
            if (this.ownBlock == null || this.ownBlock.Code.Path.Contains("clay"))
            { return null; }

            var mesh = this.ownBlock.GenMesh(this.Api as ICoreClientAPI, this.GetContent(), this.Pos);
            return mesh;
        }


        public ItemStack GetContent()
        { return this.inv[0].Itemstack; }


        internal void SetContent(ItemStack stack)
        {
            this.inv[0].Itemstack = stack;
            this.MarkDirty(true);
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (this.currentMesh == null || this.ownBlock.Code.Path.Contains("clay"))
            { return false; }
            mesher.AddMeshData(this.currentMesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, 0, 0));
            return true;
        }
    }
}
