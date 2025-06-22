using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    public class BlockBottle : BlockLiquidContainerBase, IContainedMeshSource, IContainedCustomName
    {
        private LiquidTopOpenContainerProps props = new();
        protected virtual string MeshRefsCacheKey => Code.ToShortString() + "meshRefs";
        protected virtual AssetLocation EmptyShapeLoc => props.EmptyShapeLoc ?? Shape.Base;
        protected virtual AssetLocation ContentShapeLoc => props.OpaqueContentShapeLoc;
        protected virtual AssetLocation LiquidContentShapeLoc => props.LiquidContentShapeLoc;
        public override float TransferSizeLitres => props.TransferSizeLitres;
        public override float CapacityLitres => props.CapacityLitres;
        public override bool CanDrinkFrom => Attributes["canDrinkFrom"].AsBool(true);
        public override bool IsTopOpened => Attributes["isTopOpened"].AsBool(true);
        public override bool AllowHeldLiquidTransfer => Attributes["allowHeldLiquidTransfer"].AsBool(true);
        protected virtual bool IsClear => Attributes["isClear"].AsBool();
        public virtual float MinFillY => Attributes["minFill"].AsFloat();
        public virtual float MaxFillY => Attributes["maxFill"].AsFloat();
        public virtual float MinFillZ => Attributes["minFillSideways"].AsFloat();
        public virtual float MaxFillZ => Attributes["maxFillSideways"].AsFloat();

        public override byte[]? GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack? stack = null)
        {
            return GetContent(stack)?.Item?.LightHsv ?? base.GetLightHsv(blockAccessor, pos, stack);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            props = Attributes?["liquidContainerProps"]?.AsObject(props, Code.Domain) ?? props;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (!IsClear && !IsTopOpened) return;

            Dictionary<int, MultiTextureMeshRef> meshrefs;
            if (capi.ObjectCache.TryGetValue(MeshRefsCacheKey, out var obj))
            {
                meshrefs = obj as Dictionary<int, MultiTextureMeshRef> ?? [];
            }
            else capi.ObjectCache[MeshRefsCacheKey] = meshrefs = [];

            if (GetContent(itemstack) is not ItemStack contentStack) return;

            var hashcode = (contentStack.StackSize + "x" + contentStack.Collectible.Code.ToShortString()).GetHashCode();
            if (!meshrefs.TryGetValue(hashcode, out var meshRef))
            {
                meshrefs[hashcode] = meshRef = capi.Render.UploadMultiTextureMesh(GenMesh(capi, contentStack));
            }

            renderinfo.ModelRef = meshRef;
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            if (api is not ICoreClientAPI capi) return;

            if (capi.ObjectCache.TryGetValue(MeshRefsCacheKey, out var obj))
            {
                foreach (var val in obj as Dictionary<int, MultiTextureMeshRef> ?? []) val.Value.Dispose();

                capi.ObjectCache.Remove(MeshRefsCacheKey);
            }
        }

        public MeshData? GenMesh(ICoreClientAPI? capi, ItemStack? contentStack, bool isSideways = false, BlockPos? forBlockPos = null)
        {
            if (capi?.Assets.TryGet(EmptyShapeLoc.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json")) is not IAsset asset) return new MeshData();

            capi.Tesselator.TesselateShape(this, asset.ToObject<Shape>(), out var mesh, new(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
            if (contentStack != null && (IsClear || IsTopOpened))
            {
                if (GetContainableProps(contentStack) is WaterTightContainableProps props)
                {
                    float fullness = contentStack.StackSize / props.ItemsPerLitre;
                    Shape? shape = capi.Assets.TryGet((props.IsOpaque ? ContentShapeLoc : LiquidContentShapeLoc).CopyWithPathPrefixAndAppendixOnce("shapes/", ".json"))?.ToObject<Shape>();
                    if (shape == null) return mesh;
                    shape = SliceFlattenedShape(shape.FlattenElementHierarchy(), fullness, isSideways);

                    var bottleMesh = mesh;
                    capi.Tesselator.TesselateShape("bottle", shape, out mesh, new BottleTextureSource(capi, contentStack, props.Texture, this), new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
                    for (int i = 0; i < mesh.Flags.Length; i++) mesh.Flags[i] = mesh.Flags[i] & ~(1 << 12); // Remove water waving flag

                    mesh.AddMeshData(bottleMesh);

                    // Water flags
                    if (forBlockPos != null)
                    {
                        mesh.CustomInts = new CustomMeshDataPartInt(mesh.FlagsCount) { Count = mesh.FlagsCount };
                        mesh.CustomInts.Values.Fill(0x4000000); // light foam only
                        mesh.CustomFloats = new CustomMeshDataPartFloat(mesh.FlagsCount * 2) { Count = mesh.FlagsCount * 2 };
                    }
                }
                else ACulinaryArtillery.logger?.Error($"Bottle with Item {contentStack.Item.Code} does not have waterTightProps and will not render any liquid inside it.");
            }
            return mesh;
        }

        // Works only if the shape hierarchy has been flattened, it must not have any element with children - Thanks for the code, Jayu!
        public Shape SliceFlattenedShape(Shape fullShape, float fullness, bool isSideways)
        {
            int axis = isSideways ? 2 : 1;
            var min = isSideways ? MinFillZ : MinFillY;
            var max = isSideways ? MaxFillZ : MaxFillY;

            var newMax = min + (max - min) * fullness;
            var newElements = new List<ShapeElement>();

            double elementMin, elementMax, adjustedFrom, adjustedTo;
            double originalHeight, newHeight, heightProportion;
            double vMin, vMax, vRange;
            foreach (var element in fullShape.Elements)
            {
                elementMin = Math.Min(element.From[axis], element.To[axis]);
                elementMax = Math.Max(element.From[axis], element.To[axis]);

                if (elementMax < min || elementMin > newMax) continue;

                var newElement = element.Clone();
                adjustedFrom = Math.Max(element.From[axis], 0);
                adjustedTo = Math.Min(element.To[axis], newMax);
                if (!(adjustedFrom <= adjustedTo)) continue;
                newElement.From[axis] = adjustedFrom;
                newElement.To[axis] = adjustedTo;

                // Calculate the proportion of the adjustment
                originalHeight = elementMax - elementMin;
                newHeight = adjustedTo - adjustedFrom;
                heightProportion = originalHeight > 0 ? newHeight / originalHeight : 0;

                for (var i = 0; i < 4; i++)
                {
                    var face = newElement.FacesResolved[i];
                    if (face != null)
                    {
                        vMin = face.Uv[1];
                        vMax = face.Uv[3];
                        vRange = vMax - vMin;

                        // Adjust the V values based on the height proportion
                        face.Uv[1] = (float)(vMin + vRange * (1 - heightProportion));
                        face.Uv[3] = (float)vMax;
                    }
                }
                if (isSideways)
                {
                    newElement.RotationOrigin = [8.0, 0.2, 8.0];
                    newElement.RotationY = 180;
                }
                newElements.Add(newElement);
            }

            var partialShape = fullShape.Clone();
            partialShape.Elements = [.. newElements];
            return partialShape;
        }

        public MeshData? GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos? forBlockPos = null)
        {
            if (forBlockPos != null && GetBlockEntity<BlockEntityBottleRack>(forBlockPos) != null)
            {
                return GenMesh(api as ICoreClientAPI, GetContent(itemstack), true, forBlockPos);
            }
            return GenMesh(api as ICoreClientAPI, GetContent(itemstack), false, forBlockPos);
        }

        public string GetMeshCacheKey(ItemStack itemstack)
        {
            var contentStack = GetContent(itemstack);
            return itemstack.Collectible.Code.ToShortString() + "-" + contentStack?.StackSize + "x" + contentStack?.Collectible.Code.ToShortString();
        }

        public string GetContainedInfo(ItemSlot inSlot)
        {
            float litres = GetCurrentLitres(inSlot.Itemstack);
            ItemStack? contentStack = GetContent(inSlot.Itemstack);

            if (contentStack == null || litres <= 0) return Lang.GetWithFallback("contained-empty-container", "{0} (Empty)", inSlot.Itemstack.GetName());

            string incontainername = Lang.Get(contentStack.Collectible.Code.Domain + ":incontainer-" + contentStack.Class.ToString().ToLowerInvariant() + "-" + contentStack.Collectible.Code.Path);

            return Lang.Get("contained-liquidcontainer-compact", inSlot.Itemstack.GetName(), litres, incontainername, PerishableInfoCompactContainer(api, inSlot));
        }


        public string GetContainedName(ItemSlot inSlot, int quantity)
        {
            return inSlot.Itemstack.GetName();
        }

        public override int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority)
        {
            if (priority == EnumMergePriority.DirectMerge && sourceStack.ItemAttributes?["canSealBottle"]?.AsBool() == true && Variant["type"] != "corked")
            {
                return 1;
            }

            return base.GetMergableQuantity(sinkStack, sourceStack, priority);
        }

        public override void TryMergeStacks(ItemStackMergeOperation op)
        {
            ItemSlot sourceSlot = op.SourceSlot;

            if (Variant["type"] != "corked" && op.CurrentPriority == EnumMergePriority.DirectMerge && sourceSlot.Itemstack.ItemAttributes?["canSealBottle"]?.AsBool() == true)
            {
                ItemSlot sinkSlot = op.SinkSlot;
                ItemStack newBottle = new(op.World.GetBlock(sinkSlot.Itemstack.Collectible.CodeWithVariant("type", "corked"))) { Attributes = sinkSlot.Itemstack.Attributes };

                if (sinkSlot.StackSize == 1) sinkSlot.Itemstack = newBottle;
                else
                {
                    sinkSlot.TakeOut(1);
                    if (!op.ActingPlayer.InventoryManager.TryGiveItemstack(newBottle, true))
                    {
                        op.World.SpawnItemEntity(newBottle, op.ActingPlayer.Entity.Pos.AsBlockPos);
                    }
                }
                op.MovedQuantity = 1;
                sourceSlot.TakeOut(1);
                sinkSlot.MarkDirty();

                return;
            }

            base.TryMergeStacks(op);
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if ((byEntity as EntityPlayer)?.Player is IPlayer plr && blockSel == null && entitySel == null && Variant["type"] == "corked")
            {
                if (plr.InventoryManager?.OffhandHotbarSlot is ItemSlot offSlot && (offSlot.Empty || offSlot.Itemstack.Collectible.FirstCodePart() == "cork"))
                {
                    ItemStack newBottle = new(byEntity.World.GetBlock(CodeWithVariant("type", "fired"))) { Attributes = itemslot.Itemstack.Attributes };

                    if (itemslot.StackSize == 1) itemslot.Itemstack = newBottle;
                    else
                    {
                        itemslot.TakeOut(1);
                        if (!plr.InventoryManager.TryGiveItemstack(newBottle, true))
                        {
                            byEntity.World.SpawnItemEntity(newBottle, byEntity.Pos.AsBlockPos);
                        }
                    }

                    ItemStack cork = new(byEntity.World.GetItem("aculinaryartillery:cork-generic"));
                    if (new DummySlot(cork).TryPutInto(byEntity.World, offSlot) <= 0)
                    {
                        byEntity.World.SpawnItemEntity(cork, byEntity.Pos.AsBlockPos);
                    }
                }
                else (api as ICoreClientAPI)?.TriggerIngameError(this, "fulloffhandslot", Lang.Get("aculinaryartillery:bottle-fulloffhandslot"));
            }

            base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }

        protected override bool tryEatStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, ItemStack? content = null)
        {
            if (GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity) == null) return false;

            var pos = byEntity.Pos.AheadCopy(0.4f).XYZ;
            pos.X += byEntity.LocalEyePos.X;
            pos.Y += byEntity.LocalEyePos.Y - 0.4f;
            pos.Z += byEntity.LocalEyePos.Z;

            if (secondsUsed > 0.5f && (int)(30 * secondsUsed) % 7 == 1)
            {
                byEntity.World.SpawnCubeParticles(pos, GetContent(slot.Itemstack), 0.3f, 4, 0.5f, (byEntity as EntityPlayer)?.Player);
            }

            if (byEntity.World is IClientWorldAccessor)
            {
                var tf = new ModelTransform();
                tf.EnsureDefaultValues();
                tf.Origin.Set(0f, 0, 0f);

                if (secondsUsed > 0.5f)
                {
                    tf.Translation.Y = Math.Min(0.02f, GameMath.Sin(20 * secondsUsed) / 10);
                }
                tf.Translation.X -= Math.Min(1f, secondsUsed * 4 * 1.57f);
                tf.Translation.Y -= Math.Min(0.05f, secondsUsed * 2);
                tf.Rotation.X += Math.Min(30f, secondsUsed * 350);
                tf.Rotation.Y += Math.Min(80f, secondsUsed * 350);
                return secondsUsed <= 1f;
            }

            return true;
        }

        protected override void tryEatStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity)
        {
            if (secondsUsed < 0.95f || byEntity.World is not IServerWorldAccessor) return;
            if (GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity) is not FoodNutritionProperties nutriProps) return;

            var litres = GetCurrentLitres(slot.Itemstack);
            var litresToDrink = litres >= 0.25f ? 0.25f : litres;

            var state = UpdateAndGetTransitionState(api.World, slot, EnumTransitionType.Perish);

            var litresMult = litres == 1 ? 0.25f : (litres == 0.75 ? 0.3333f : (litres == 0.5 ? 0.5f : 1.0f));

            byEntity.ReceiveSaturation(nutriProps.Satiety * litresMult * GlobalConstants.FoodSpoilageSatLossMul(state?.TransitionLevel ?? 0, slot.Itemstack, byEntity), nutriProps.FoodCategory);
            IPlayer? player = (byEntity as EntityPlayer)?.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

            SplitStackAndPerformAction(byEntity, slot, (stack) => TryTakeLiquid(stack, litresToDrink)?.StackSize ?? 0);

            if (nutriProps.Intoxication > 0f)
            {
                var intox = byEntity.WatchedAttributes.GetFloat("intoxication");
                byEntity.WatchedAttributes.SetFloat("intoxication", Math.Min(litresToDrink, intox + (nutriProps.Intoxication * litresMult)));
            }

            var healthMod = nutriProps.Health * litresMult * GlobalConstants.FoodSpoilageHealthLossMul(state?.TransitionLevel ?? 0, slot.Itemstack, byEntity);
            if (healthMod != 0) byEntity.ReceiveDamage(new() { Source = EnumDamageSource.Internal, Type = healthMod > 0 ? EnumDamageType.Heal : EnumDamageType.Poison }, Math.Abs(healthMod));

            slot.MarkDirty();
            player?.InventoryManager.BroadcastHotbarSlot();

            if (GetCurrentLitres(slot.Itemstack) == 0) SetContent(slot.Itemstack, null); //null it out

            return;
        }

        public override float GetContainingTransitionModifierContained(IWorldAccessor world, ItemSlot inSlot, EnumTransitionType transType)
        {
            return Attributes[transType == EnumTransitionType.Perish ? "perishRate" : "cureRate"].AsFloat(1);
        }

        public float SatMult=> Attributes?["satMult"].AsFloat(1f) ?? 1f;

        public FoodNutritionProperties[]? GetPropsFromArray(float[]? satieties)
        {
            if (satieties == null || satieties.Length < 6) return null;

            List<FoodNutritionProperties> props = [];
            for (int i = 1; i <= 5; i++)
            {
                if (satieties[i] != 0) props.Add(new() { FoodCategory = (EnumFoodCategory)(i - 1), Satiety = satieties[i] * SatMult });
            }

            if (satieties[0] != 0 && props.Count > 0) props[0].Health = satieties[0] * SatMult;

            return [.. props];
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (GetContent(inSlot.Itemstack) is ItemStack content)
            {
                string newDescription = content.Collectible.Code.Domain + ":itemdesc-" + content.Collectible.Code.Path;
                string finalDescription = Lang.GetMatching(newDescription);

                var dummy = new DummySlot(content);

                if (finalDescription != newDescription)
                {
                    dsc.AppendLine();
                    dsc.Append(finalDescription);
                }

                EntityPlayer? entity = (world as IClientWorldAccessor)?.Player.Entity;
                float spoilState = AppendPerishableInfoText(dummy, new StringBuilder(), world);

                var nutriProps = ItemExpandedRawFood.GetExpandedContentNutritionProperties(world, dummy, content, entity);

                FoodNutritionProperties[]? addProps = GetPropsFromArray((content.Attributes["expandedSats"] as FloatArrayAttribute)?.value);

                if (nutriProps != null && addProps?.Length > 0)
                {
                    dsc.AppendLine();
                    dsc.AppendLine(Lang.Get("efrecipes:Extra Nutrients"));

                    foreach (FoodNutritionProperties props in addProps)
                    {
                        double liquidVolume = content.StackSize;
                        float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, content, entity);
                        float healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, content, entity);

                        if (Math.Abs(props.Health * healthLossMul) > 0.001f)
                        {
                            dsc.AppendLine(Lang.Get("efrecipes:- {0} {2} sat, {1} hp", Math.Round(props.Satiety * satLossMul * (liquidVolume / 10), 1), props.Health * healthLossMul * (liquidVolume / 10), props.FoodCategory.ToString()));
                        }
                        else
                        {
                            dsc.AppendLine(Lang.Get("efrecipes:- {0} {1} sat", Math.Round(props.Satiety * satLossMul * (liquidVolume / 10)), props.FoodCategory.ToString()));
                        }
                    }
                }
            }
        }

        public override void OnGroundIdle(EntityItem entityItem)
        {
            base.OnGroundIdle(entityItem);

            if (!entityItem.Swimming || entityItem.World.Side != EnumAppSide.Server) return;

            var contents = GetContent(entityItem.Itemstack);
            if (contents?.Collectible.Code.Path == "rot")
            {
                entityItem.World.SpawnItemEntity(contents, entityItem.ServerPos.XYZ);
                SetContent(entityItem.Itemstack, null);
            }
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return
            [
                new()
                {
                    ActionLangCode = "heldhelp-empty",
                    HotKeyCode = "sprint",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => GetCurrentLitres(inSlot.Itemstack) > 0,
                },
                 new()
                {
                    ActionLangCode = "aculinaryartillery:heldhelp-drink",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => (GetContent(inSlot.Itemstack)?.GetName() is not null and not "Water") && GetCurrentLitres(inSlot.Itemstack) > 0,
                },
                new()
                {
                    ActionLangCode = "heldhelp-fill",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => bs != null && (api.World.BlockAccessor.GetBlock(bs.Position.AddCopy(bs.Face))?.Code.GetName().Contains("water-") == true) && GetCurrentLitres(inSlot.Itemstack) == 0,
                },
                new()
                {
                    ActionLangCode = "heldhelp-place",
                    HotKeyCode = "sneak",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => true
                }
            ];
        }
    }

    /*************************************************************************************************************/
    public class BottleTextureSource : ITexPositionSource
    {
        public ItemStack forContents;
        private readonly ICoreClientAPI capi;
        private TextureAtlasPosition? contentTextPos;
        private readonly TextureAtlasPosition blockTextPos;
        private readonly TextureAtlasPosition corkTextPos;
        private readonly CompositeTexture contentTexture;

        public BottleTextureSource(ICoreClientAPI capi, ItemStack forContents, CompositeTexture contentTexture, Block bottle)
        {
            this.capi = capi;
            this.forContents = forContents;
            this.contentTexture = contentTexture;
            this.corkTextPos = capi.BlockTextureAtlas.GetPosition(bottle, "map");
            this.blockTextPos = capi.BlockTextureAtlas.GetPosition(bottle, "glass");
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "map" && corkTextPos != null) return corkTextPos;
                if (textureCode == "glass" && blockTextPos != null) return blockTextPos;

                if (contentTextPos == null)
                {
                    int textureSubId;
                    textureSubId = ObjectCacheUtil.GetOrCreate(capi, "contenttexture-" + contentTexture?.ToString() ?? "unknowncontent", () =>
                    {
                        var id = 0;
                        var bmp = capi.Assets.TryGet(contentTexture?.Base.CopyWithPathPrefixAndAppendixOnce("textures/", ".png") ?? new AssetLocation("aculinaryartillery:textures/block/unknown.png"))?.ToBitmap(capi);

                        if (bmp != null)
                        {
                            if (contentTexture != null && contentTexture.Alpha != 255)
                            {
                                bmp.MulAlpha(contentTexture.Alpha);
                            }

                            capi.BlockTextureAtlas.InsertTexture(bmp, out id, out var texPos);
                            bmp.Dispose();
                        }
                        return id;
                    });

                    contentTextPos = capi.BlockTextureAtlas.Positions[textureSubId];
                }

                return contentTextPos;
            }
        }
        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
    }
}