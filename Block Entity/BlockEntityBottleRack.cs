namespace ACulinaryArtillery
{
    using System;
    using System.Text;
    using Vintagestory.API.Client;
    using Vintagestory.API.Common;
    using Vintagestory.API.MathTools;
    using Vintagestory.GameContent;
    using Vintagestory.API.Datastructures;
    using Vintagestory.API.Config;
    using Vintagestory.API.Util;
    //using System.Diagnostics;

    public class BlockEntityBottleRack : BlockEntityDisplayCase, ITexPositionSource
    {
        private readonly int maxSlots = 16;
        public override string InventoryClassName => "bottlerack";
        //protected InventoryGeneric inventory;

        public override InventoryBase Inventory => this.inventory;


        public BlockEntityBottleRack()
        {
            this.inventory = new InventoryGeneric(this.maxSlots, null, null);
            //this.meshes = new MeshData[this.maxSlots]; 1.18
            var meshes = new MeshData[this.maxSlots];
        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            var playerSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (playerSlot.Empty)
            {
                if (this.TryTake(byPlayer, blockSel))
                { return true; }
                return false;
            }
            else
            {
                var colObj = playerSlot.Itemstack.Collectible;
                if (colObj.Attributes != null)
                {
                    if (colObj.Code.Path.StartsWith("bottle-"))
                    {
                        if (this.TryPut(playerSlot, blockSel))
                        {
                            var sound = this.Block?.Sounds?.Place;
                            this.Api.World.PlaySoundAt(sound ?? new AssetLocation("game:sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                            return true;
                        }
                    }
                    return false;
                }
            }
            return false;
        }

        internal void OnBreak(IPlayer byPlayer, BlockPos pos)
        {
            for (var index = 15; index >= 0; index--)
            {
                if (!this.inventory[index].Empty)
                {
                    var stack = this.inventory[index].TakeOut(1);
                    if (stack.StackSize > 0)
                    { this.Api.World.SpawnItemEntity(stack, this.Pos.ToVec3d().Add(0.5, 0.5, 0.5)); }
                    this.MarkDirty(true);
                }
            }
        }

        private bool TryPut(ItemSlot slot, BlockSelection blockSel)
        {
            var index = blockSel.SelectionBoxIndex;
            if (this.inventory[index].Empty)
            {
                var moved = slot.TryPutInto(this.Api.World, this.inventory[index]);
                if (moved > 0)
                {
                    this.updateMesh(index);
                    this.MarkDirty(true);
                }
                return moved > 0;
            }
            return false;
        }

        private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
        {
            var index = blockSel.SelectionBoxIndex;
            if (!this.inventory[index].Empty)
            {
                var stack = this.inventory[index].TakeOut(1);
                if (byPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    var sound = stack.Block?.Sounds?.Place;
                    this.Api.World.PlaySoundAt(sound ?? new AssetLocation("game:sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                }
                if (stack.StackSize > 0)
                {
                    this.Api.World.SpawnItemEntity(stack, this.Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
                this.updateMesh(index);
                this.MarkDirty(true);
                return true;
            }
            return false;
        }

        /* public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolve)
        {
            base.FromTreeAttributes(tree, worldForResolve);
            if (this.Api != null)
            {
                if (this.Api.Side == EnumAppSide.Client)
                {
                    this.UpdateMeshes();
                    this.Api.World.BlockAccessor.MarkBlockDirty(this.Pos);
                }
            }
        }

        public virtual void UpdateMeshes()
        {
            for (var i = 0; i < this.meshes.Length; i++)
            {
                this.UpdateMesh(i);
            }
        }

        protected virtual void UpdateMesh(int index)
        {
            if (this.Api == null || this.Api.Side == EnumAppSide.Server)
                return;
            if (this.Inventory[index].Empty)
            {
                this.meshes[index] = null;
                return;
            }

            var mesh = this.GenMesh(this.Inventory[index].Itemstack);
            this.TranslateMesh(mesh, index);
            this.meshes[index] = mesh;
        } */

        protected virtual MeshData GenMesh(ItemStack stack)
        {
            MeshData mesh;

            if (stack.Collectible is IContainedMeshSource dynBlock)
            {
                mesh = dynBlock.GenMesh(stack, this.capi.BlockTextureAtlas, this.Pos);
                mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, this.Block.Shape.rotateY * GameMath.DEG2RAD, 0);
            }
            else
            {
                var capi = this.Api as ICoreClientAPI;
                if (stack.Class == EnumItemClass.Block)
                {
                    mesh = capi.TesselatorManager.GetDefaultBlockMesh(stack.Block).Clone();
                }
                else
                {
                    this.nowTesselatingObj = stack.Collectible;
                    this.nowTesselatingShape = null;
                    if (stack.Item.Shape != null)
                    {
                        this.nowTesselatingShape = capi.TesselatorManager.GetCachedShape(stack.Item.Shape.Base);
                    }
                    capi.Tesselator.TesselateItem(stack.Item, out mesh, this);

                    mesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.BlendNoCull);
                }
            }

            if (stack.Collectible.Attributes?[this.AttributeTransformCode].Exists == true)
            {
                var transform = stack.Collectible.Attributes?[this.AttributeTransformCode].AsObject<ModelTransform>();
                transform.EnsureDefaultValues();
                mesh.ModelTransform(transform);

                transform.Rotation.X = 0;
                transform.Rotation.Y = this.Block.Shape.rotateY;
                transform.Rotation.Z = 0;
                mesh.ModelTransform(transform);
            }

            if (stack.Class == EnumItemClass.Item && (stack.Item.Shape == null || stack.Item.Shape.VoxelizeTexture))
            {
                mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), GameMath.PIHALF, 0, 0);
                mesh.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.33f, 0.5f, 0.33f);
                mesh.Translate(0, -7.5f / 16f, 0f);
            }
            return mesh;
        }




        public MeshData TransformBottleMesh(MeshData mesh, int slot, string type, string direction)
        {
            var rot = 0f; //north in radians
            switch (direction)
            {
                case "east": rot = 1.57f; break;
                case "south": rot = 3.14f; break;
                case "west": rot = 4.71f; break;
                default: break;
            }
            double col = slot % 4;
            var x = (float)col / 4 - 0.38f;
            var y = (float)(Math.Floor(slot / 4f) / 4f) - 0.3f;
            mesh.Translate(x, y - 0.5f, -0.42f);
            if (type == "bottlerackcorner")
            {
                if (col == 1 || col == 2)
                { mesh.Translate(0f, 0.2f, 0.2f); }
            }

            mesh.Rotate(new Vec3f(0.5f, y, 0.5f), 1.57f, 0f, rot);
            mesh.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.99f, 0.99f, 0.99f);
            if (type == "bottlerackcorner")
            {
                if (col == 1 || col == 2)
                {
                    mesh.Translate(0f, 0.2f, 0f);
                    mesh.Rotate(new Vec3f(0.5f, y, 0.5f), 0f, -0.785f, 0f);
                }
                else if (col == 3) //far right column
                { mesh.Rotate(new Vec3f(0.5f, y, 0.5f), 0f, -1.57f, 0f); }
            }
            return mesh;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            MeshData mesh;
            var shapeBase = "aculinaryartillery:shapes/";
            var block = this.Api.World.BlockAccessor.GetBlock(this.Pos) as BlockBottleRack;
            mesh = this.capi.TesselatorManager.GetDefaultBlockMesh(block); //add bottle rack
            mesher.AddMeshData(mesh);
            for (var i = 0; i <= 15; i++)
            {
                if (!this.inventory[i].Empty)
                {
                    var blockPath = this.inventory[i].Itemstack.Block.Code.Path;
                    if (blockPath.Contains("-clay-"))
                    {
                        var bottleBlock = this.Api.World.GetBlock(block.CodeWithPath(blockPath));
                        var texture = ((ICoreClientAPI)this.Api).Tesselator.GetTexSource(bottleBlock);
                        mesh = block.GenMesh(this.Api as ICoreClientAPI, shapeBase + "block/bottle/bottle", texture, tesselator);
                        mesh = this.TransformBottleMesh(mesh, i, block.FirstCodePart(), block.LastCodePart());
                        mesher.AddMeshData(mesh);
                    }
                    else
                    {
                        var content = (this.inventory[i].Itemstack.Collectible as BlockBottle).GetContent(this.inventory[i].Itemstack);
                        if (content != null) //glass bottle with contents
                        {
                            mesh = (this.inventory[i].Itemstack.Collectible as BlockBottle).GenMeshSideways(this.Api as ICoreClientAPI, content, this.Pos);
                            mesh = this.TransformBottleMesh(mesh, i, block.FirstCodePart(), block.LastCodePart());
                            mesher.AddMeshData(mesh);
                        }
                        else //glass bottle
                        {
                            var bottleBlock = this.inventory[i].Itemstack.Block as BlockBottle;
                            var texture = tesselator.GetTexSource(bottleBlock);
                            mesh = block.GenMesh(this.Api as ICoreClientAPI, shapeBase + "block/bottle/glassbottleempty", texture, tesselator);
                            mesh = this.TransformBottleMesh(mesh, i, block.FirstCodePart(), block.LastCodePart());
                            mesher.AddMeshData(mesh);
                        }
                    }
                }
            }
            return true;
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            sb.AppendLine(Lang.Get("Suitable spot for liquid storage."));
            sb.AppendLine();
            if (forPlayer?.CurrentBlockSelection == null)
            { return; }
            var index = forPlayer.CurrentBlockSelection.SelectionBoxIndex;
            if (!this.inventory[index].Empty)
            {
                sb.AppendLine(this.inventory[index].Itemstack.GetName());
#if FALSE
                // TODO: decide if we *really* want to block display of items in racks when made out of clay...
                var blockPath = this.inventory[index].Itemstack.Block.Code.Path;
                if (!blockPath.Contains("-clay-")) {
#endif
                (this.inventory[index].Itemstack.Collectible as BlockLiquidContainerBase)?.GetContentInfo(this.inventory[index], sb, Api.World);
#if FALSE
}
#endif
            }
        }
    }
}
