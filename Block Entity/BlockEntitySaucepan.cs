using ACulinaryArtillery.Block_Entity;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    public class BlockEntitySaucepan : BlockEntityBucket
    {
        MeshData currentRightMesh;
        BlockSaucepan ownBlock;
        public bool isSealed;
        public BlockEntitySaucepan()
        {
            container = new BESaucepanContainer(this,() => Inventory, "inventory");
        }
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            //Inventory.OnAcquireTransitionSpeed += Inventory_OnAcquireTransitionSpeed;
            ownBlock = Block as BlockSaucepan;


            if (Api.Side == EnumAppSide.Client)
            {
                currentRightMesh = GenRightMesh();
                MarkDirty(true);
            }
        }

        /*
        private float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float mulByConfig)
        {
            float rate = container.GetPerishRate() * (isSealed ? Block.Attributes["lidPerishRate"].AsFloat(0.5f) : 1f);
            float mul = Inventory.GetTransitionSpeedMul(transType, stack);
            return mul * rate;

        }
        */

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (byItemStack != null) isSealed = byItemStack.Attributes.GetBool("isSealed");

            if (Api.Side == EnumAppSide.Client)
            {
                currentRightMesh = GenRightMesh();
                MarkDirty(true);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            MeshAngle = tree.GetFloat("meshAngle", MeshAngle);
            isSealed = tree.GetBool("isSealed");

            if (Api?.Side == EnumAppSide.Client)
            {
                currentRightMesh = GenRightMesh();
                MarkDirty(true);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("isSealed", isSealed);
        }

        public SimmerRecipe GetMatchingSimmerRecipe(IWorldAccessor world, ItemSlot[] slots)
        {
            List<SimmerRecipe> recipes = Api.GetSimmerRecipes();

            return recipes.FirstOrDefault(r => r.Matches(world, slots));
        }

        internal MeshData GenRightMesh()
        {
            if (ownBlock == null || ownBlock.Code.Path.Contains("clay")) return null;

            MeshData mesh = ownBlock.GenRightMesh(Api as ICoreClientAPI, GetContent(), Pos, isSealed);

            if (mesh.CustomInts != null)
            {
                for (int i = 0; i < mesh.CustomInts.Count; i++)
                {
                    mesh.CustomInts.Values[i] |= 1 << 27; // Disable water wavy
                    mesh.CustomInts.Values[i] |= 1 << 26; // Enabled weak foam
                }
            }

            return mesh;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(currentRightMesh);
            return true;
        }

        public void RedoMesh()
        {
            if (Api.Side == EnumAppSide.Client)
            {
                currentRightMesh = GenRightMesh();
            }
        }

        /*
        public override float GetPerishRate()
        {
            return base.GetPerishRate() * (isSealed ? Block.Attributes["lidPerishRate"].AsFloat(0.5f) : 1f);
        }
        */
    }
}
