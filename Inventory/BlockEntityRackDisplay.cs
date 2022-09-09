using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
        public abstract class BlockEntityRackDisplay : BlockEntityContainer, ITexPositionSource
        {
            protected Vintagestory.API.Common.Item nowTesselatingItem;
            protected Shape nowTesselatingShape;
            protected ICoreClientAPI capi;
            protected MeshData[] meshes;
            protected MealMeshCache ms;

            public Size2i AtlasSize => this.capi.BlockTextureAtlas.Size;

            public virtual string AttributeTransformCode => "onDisplayTransform";

            public TextureAtlasPosition this[string textureCode]
            {
                get
                {
                    AssetLocation assetLocation = (AssetLocation)null;
                    CompositeTexture compositeTexture;
                    if (this.nowTesselatingItem.Textures.TryGetValue(textureCode, out compositeTexture))
                        assetLocation = compositeTexture.Baked.BakedName;
                    else if (this.nowTesselatingItem.Textures.TryGetValue("all", out compositeTexture))
                        assetLocation = compositeTexture.Baked.BakedName;
                    else
                        this.nowTesselatingShape?.Textures.TryGetValue(textureCode, out assetLocation);
                    if (assetLocation == null)
                        assetLocation = new AssetLocation(textureCode);
                    TextureAtlasPosition texPos = this.capi.BlockTextureAtlas[assetLocation];
                    if (texPos == null)
                    {
                        IAsset asset = this.capi.Assets.TryGet(assetLocation.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                        if (asset != null)
                        {
                            BitmapRef bitmap = asset.ToBitmap(this.capi);
                            this.capi.BlockTextureAtlas.GetOrInsertTexture(assetLocation, out int _, out texPos);
                        }
                        else
                            this.capi.World.Logger.Warning("For render in block " + this.Block.Code?.ToString() + ", item {0} defined texture {1}, not no such texture found.", (object)this.nowTesselatingItem.Code, (object)assetLocation);
                    }
                    return texPos;
                }
            }

            public override void Initialize(ICoreAPI api)
            {
                base.Initialize(api);
                this.ms = api.ModLoader.GetModSystem<MealMeshCache>();
                this.capi = api as ICoreClientAPI;
                if (this.capi == null)
                    return;
                this.updateMeshes();
            }

            public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
            {
                for (int index = 0; index < this.meshes.Length; ++index)
                {
                    if (this.meshes[index] != null)
                        mesher.AddMeshData(this.meshes[index]);
                }
                return false;
            }

            public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
            {
                base.FromTreeAttributes(tree, worldForResolving);
                if (worldForResolving.Side != EnumAppSide.Client || this.Api == null)
                    return;
                this.updateMeshes();
            }

            protected virtual void updateMeshes()
            {
                for (int index = 0; index < this.meshes.Length; ++index)
                    this.updateMesh(index);
            }

            protected virtual void updateMesh(int index)
            {
                if (this.Api == null || this.Api.Side == EnumAppSide.Server)
                    return;
                if (this.Inventory[index].Empty)
                {
                    this.meshes[index] = (MeshData)null;
                }
                else
                {
                    MeshData mesh = this.genMesh(this.Inventory[index].Itemstack, index);
                    this.translateMesh(mesh, index);
                    this.meshes[index] = mesh;
                }
            }

            protected virtual MeshData genMesh(ItemStack stack, int index)
            {
                ICoreClientAPI api = this.Api as ICoreClientAPI;
                MeshData modeldata;
                if (stack.Class == EnumItemClass.Block)
                {
                    modeldata = !(stack.Block is BlockPie) ? api.TesselatorManager.GetDefaultBlockMesh(stack.Block).Clone() : this.ms.GetPieMesh(stack);
                }
                else
                {
                    this.nowTesselatingItem = stack.Item;
                    if (stack.Item.Shape != null)
                        this.nowTesselatingShape = api.TesselatorManager.GetCachedShape(stack.Item.Shape.Base);
                    api.Tesselator.TesselateItem(stack.Item, out modeldata, (ITexPositionSource)this);
                    JsonObject attributes = stack.Collectible.Attributes;
                    if ((attributes != null ? (attributes[this.AttributeTransformCode].Exists ? 1 : 0) : 0) != 0)
                    {
                        ModelTransform transform = stack.Collectible.Attributes?[this.AttributeTransformCode].AsObject<ModelTransform>();
                        transform.EnsureDefaultValues();
                        modeldata.ModelTransform(transform);
                    }
                    if (stack.Item.Shape == null || stack.Item.Shape.VoxelizeTexture)
                    {
                        modeldata.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 1.570796f, 0.0f, 0.0f);
                        modeldata.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.33f, 0.5f, 0.33f);
                        modeldata.Translate(0.0f, -15f / 32f, 0.0f);
                    }
                    modeldata.RenderPassesAndExtraBits.Fill<short>((short)2);
                }
                return modeldata;
            }

            protected virtual void translateMesh(MeshData mesh, int index)
            {
            }
        }
    }
