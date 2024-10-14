using Vintagestory.GameContent;

namespace ACulinaryArtillery.Block_Entity
{
    public class BlockEntitySaucepanContainer : InWorldContainer
    {
        private BlockEntitySaucepan blockEntitySaucepan;
        public BlockEntitySaucepanContainer(BlockEntitySaucepan blockEntitySaucepan, InventorySupplierDelegate inventorySupplier, string treeAttrKey): base(inventorySupplier, treeAttrKey)
        {
            this.blockEntitySaucepan = blockEntitySaucepan;
        }

        public override float GetPerishRate()
        {
            return GetPerishRate() * (blockEntitySaucepan.isSealed ? blockEntitySaucepan.Block.Attributes["lidPerishRate"].AsFloat(0.5f) : 1f);
        }
    }
}
