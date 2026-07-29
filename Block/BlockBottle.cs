using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
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

        public ItemStack[] corkStacks = null!;


        public override byte[]? GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack? stack = null)
        {
            return GetContent(stack)?.Item?.LightHsv ?? base.GetLightHsv(blockAccessor, pos, stack);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            props = Attributes?["liquidContainerProps"]?.AsObject(props, Code.Domain) ?? props;
            drinkPortionSizeFromAttributes = Attributes?["drinkPortionSize"].AsFloat(0.25f) ?? 0.25f; //base game reads this as an integer

            List<ItemStack> corkstacks = [];

            foreach (CollectibleObject obj in api.World.Collectibles)
            {
                if (obj.FirstCodePart() == "cork")
                {
                    corkstacks.Add(new ItemStack(obj));
                }
            }

            corkStacks = [.. corkstacks];
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<int, MultiTextureMeshRef> meshrefs;
            if (capi.ObjectCache.TryGetValue(MeshRefsCacheKey, out var obj))
            {
                meshrefs = obj as Dictionary<int, MultiTextureMeshRef> ?? [];
            }
            else
            {
                capi.ObjectCache[MeshRefsCacheKey] = meshrefs = [];
            }

            string key = itemstack.Collectible.Code.ToShortString();

            ItemStack? contentStack = GetContent(itemstack);
            if (contentStack != null)
            {
                key += "-" + contentStack?.StackSize + "x" + contentStack?.Collectible.Code.ToShortString();
            }

            ItemStack? corkStack = GetCork(itemstack);
            if (corkStack != null)
            {
                key += "-" + corkStack.Collectible.Code;
            }

            int hashcode = key.GetHashCode();
            if (!meshrefs.TryGetValue(hashcode, out var meshRef))
            {
                meshrefs[hashcode] = meshRef = capi.Render.UploadMultiTextureMesh(GenMesh(capi, itemstack));
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

        public MeshData? GenMesh(ICoreClientAPI? capi, ItemStack stack, bool isSideways = false, BlockPos? atBlockPos = null)
        {
            if (capi?.Assets.TryGet(EmptyShapeLoc.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json")) is not IAsset asset) return new MeshData();

            ItemStack? cork = stack.Collectible.LastCodePart() == "corked" ? GetCork(stack) : null;
            capi.Tesselator.TesselateShape("bottle", asset.ToObject<Shape>(), out var mesh, new BottleTextureSource(capi, stack, cork, null), new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));

            if (GetContent(stack) is ItemStack contentStack && (IsClear || IsTopOpened))
            {
                if (GetContainableProps(contentStack) is not WaterTightContainableProps props)
                {
                    ACulinaryArtillery.logger?.Error($"Bottle content stack {contentStack.Item.Code} does not have waterTightContainerProps and will not render any liquid.");
                    return mesh;
                }

                float fullness = contentStack.StackSize / props.ItemsPerLitre;
                Shape? shape = capi.Assets.TryGet((props.IsOpaque ? ContentShapeLoc : LiquidContentShapeLoc).CopyWithPathPrefixAndAppendixOnce("shapes/", ".json"))?.ToObject<Shape>();
                if (shape == null) return mesh;
                shape = SliceFlattenedShape(shape.FlattenElementHierarchy(), fullness, isSideways);

                MeshData bottleMesh = mesh;
                capi.Tesselator.TesselateShape("bottle contents", shape, out mesh, new BottleTextureSource(capi, stack, cork, props.Texture), new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
                for (int i = 0; i < mesh.Flags.Length; i++) mesh.Flags[i] = mesh.Flags[i] & ~(1 << 12); // Remove water waving flag

                mesh.AddMeshData(bottleMesh);

                // Water flags
                if (atBlockPos != null)
                {
                    mesh.CustomInts = new CustomMeshDataPartInt(mesh.FlagsCount) { Count = mesh.FlagsCount };
                    mesh.CustomInts.Values.Fill(0x4000000); // light foam only
                    mesh.CustomFloats = new CustomMeshDataPartFloat(mesh.FlagsCount * 2) { Count = mesh.FlagsCount * 2 };
                }
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

        public MeshData? GenMesh(ItemSlot slot, ITextureAtlasAPI targetAtlas, BlockPos? atBlockPos = null)
        {
            if (slot.Itemstack is not ItemStack stack) return new MeshData();

            bool isSideways = atBlockPos != null && GetBlockEntity<BlockEntityBottleRack>(atBlockPos) != null;
            return GenMesh(api as ICoreClientAPI, stack, isSideways, atBlockPos);
        }

        public string GetMeshCacheKey(ItemSlot slot)
        {
            if (slot.Itemstack is not ItemStack stack) return "";

            ItemStack? contentStack = GetContent(stack);
            string key = stack.Collectible.Code.ToShortString();

            if (contentStack != null)
            {
                key += "-" + contentStack?.StackSize + "x" + contentStack?.Collectible.Code.ToShortString();
            }

            ItemStack? corkStack = GetCork(stack);
            if (corkStack != null)
            {
                key += "-" + corkStack.Collectible.Code;
            }

            return key;
        }

        public ItemStack? GetCork(ItemStack stack) => GetContents(api.World, stack).ElementAtOrDefault(1);

        public void SetCork(ItemStack bottleStack, ItemStack? corkStack)
        {
            ItemStack?[] contentStacks = GetContents(api.World, bottleStack);
            if (contentStacks.Length > 0)
            {
                contentStacks[1] = corkStack;
            }
            else
            {
                contentStacks = [null, corkStack];
            }
            SetContents(bottleStack, contentStacks);
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
            return inSlot.Itemstack?.GetName() ?? "";
        }

        public void PlayCorkingSound(ICoreServerAPI? sapi, ItemStack bottle, Entity entity, bool isUncorking = false)
        {
            if (sapi == null) return;

            float fullness = 0;
            if (GetContent(bottle) is ItemStack contentStack
                && GetContainableProps(contentStack) is WaterTightContainableProps liquidProps)
            {
                fullness = contentStack.StackSize / liquidProps.ItemsPerLitre;
            }

            string suffix = fullness switch
            {
                0 => "empty",
                <= 0.5f => "partial",
                _ => "full"
            };
            AssetLocation sound = new($"aculinaryartillery:sounds/player/bottle/{(isUncorking ? "uncork" : "cork")}{suffix}*");
            sapi.World.PlaySoundAt(sound, entity);
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
            ItemSlot sinkSlot = op.SinkSlot;

            if (Variant["type"] != "corked" && op.CurrentPriority == EnumMergePriority.DirectMerge && sinkSlot.Itemstack != null && sourceSlot.Itemstack?.ItemAttributes?["canSealBottle"]?.AsBool() == true)
            {
                ItemStack corkedBottle = new(op.World.GetBlock(sinkSlot.Itemstack.Collectible.CodeWithVariant("type", "corked"))) { Attributes = sinkSlot.Itemstack?.Attributes };
                ItemStack cork = sourceSlot.Itemstack.Clone();
                SetCork(corkedBottle, cork);

                if (sinkSlot.StackSize == 1)
                {
                    sinkSlot.Itemstack = corkedBottle;
                }
                else
                {
                    sinkSlot.TakeOut(1);
                    if (!op.ActingPlayer.InventoryManager.TryGiveItemstack(corkedBottle, true))
                    {
                        op.World.SpawnItemEntity(corkedBottle, op.ActingPlayer.Entity.Pos.AsBlockPos);
                    }
                }
                op.MovedQuantity = 1;
                sourceSlot.TakeOut(1);
                sinkSlot.MarkDirty();

                PlayCorkingSound(api as ICoreServerAPI, sinkSlot.Itemstack, op.ActingPlayer.Entity);

                return;
            }

            base.TryMergeStacks(op);
        }

        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, IRecipeBase byRecipe)
        {
            if (byRecipe.Name?.FirstCodePart() == "uncork" && outputSlot.Itemstack != null)
            {
                ItemStack? cork = GetCork(outputSlot.Itemstack);
                SetCork(outputSlot.Itemstack, null);
            }

            if (byRecipe.Name?.FirstCodePart() == "cork" && outputSlot.Itemstack != null)
            {
                ItemStack? cork = null;
                foreach (ItemSlot slot in allInputslots)
                {
                    if (slot.Itemstack?.ItemAttributes["canSealBottle"].AsBool(false) ?? false)
                    {
                        cork = slot.Itemstack.Clone();
                        cork.StackSize = 1;
                    }
                }

                if (cork == null) return;

                SetCork(outputSlot.Itemstack, cork);
            }

            base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
        }

        public override void OnConsumedByCrafting(ItemSlot[] allInputSlots, ItemSlot stackInSlot, IRecipeBase recipe, IRecipeIngredient fromIngredient, IPlayer byPlayer, int quantity)
        {
            if (recipe.Name?.FirstCodePart() == "uncork" && stackInSlot.Itemstack != null)
            {
                ItemStack? cork = GetCork(stackInSlot.Itemstack);
                SetCork(stackInSlot.Itemstack, null);

                if (!byPlayer.InventoryManager.TryGiveItemstack(cork, true))
                {
                    byPlayer.Entity.World.SpawnItemEntity(cork, byPlayer.Entity.Pos.AsBlockPos);
                }
            }

            if (recipe.Name?.FirstCodePart() == "cork" && stackInSlot.Itemstack != null)
            {
                ItemStack? cork = null;
                foreach (ItemSlot slot in allInputSlots)
                {
                    if (slot.Itemstack?.ItemAttributes["canSealBottle"].AsBool(false) ?? false)
                    {
                        cork = slot.Itemstack.Clone();
                        cork.StackSize = 1;
                    }
                }

                SetCork(stackInSlot.Itemstack, cork);
            }

            base.OnConsumedByCrafting(allInputSlots, stackInSlot, recipe, fromIngredient, byPlayer, quantity);
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (entitySel != null) return;

            IPlayer? plr = (byEntity as EntityPlayer)?.Player;
            ItemSlot? offhandSlot = plr?.InventoryManager?.OffhandHotbarSlot;

            if (plr != null && blockSel == null && Variant["type"] == "corked" && !byEntity.Controls.ShiftKey && itemslot.Itemstack != null)
            {
                if (offhandSlot != null && (offhandSlot.Empty || offhandSlot.Itemstack.Collectible.FirstCodePart() == "cork"))
                {
                    ItemStack? cork = GetCork(itemslot.Itemstack);
                    ItemStack uncorkedBottle = new(byEntity.World.GetBlock(CodeWithVariant("type", "fired"))) { Attributes = itemslot.Itemstack.Attributes };
                    SetCork(uncorkedBottle, null);

                    if (itemslot.StackSize == 1)
                    {
                        itemslot.Itemstack = uncorkedBottle;
                    }
                    else
                    {
                        itemslot.TakeOut(1);
                        if (!plr.InventoryManager.TryGiveItemstack(uncorkedBottle, true))
                        {
                            byEntity.World.SpawnItemEntity(uncorkedBottle, byEntity.Pos.AsBlockPos);
                        }
                    }


                    if (new DummySlot(cork).TryPutInto(byEntity.World, offhandSlot) <= 0)
                    {
                        byEntity.World.SpawnItemEntity(cork, byEntity.Pos.AsBlockPos);
                    }


                    itemslot.MarkDirty();
                    offhandSlot.MarkDirty();
                    plr.InventoryManager.BroadcastHotbarSlot();

                    PlayCorkingSound(api as ICoreServerAPI, itemslot.Itemstack, byEntity, isUncorking: true);

                    handHandling = EnumHandHandling.PreventDefault;
                    return;
                }
                else (api as ICoreClientAPI)?.TriggerIngameError(this, "fulloffhandslot", Lang.Get("aculinaryartillery:bottle-fulloffhandslot"));
            }

            if (blockSel == null && byEntity.Controls.ShiftKey && plr != null && offhandSlot != null && itemslot.Itemstack != null
                && Variant["type"] == "fired"
                && offhandSlot.Itemstack?.Collectible.FirstCodePart() == "cork")
            {
                ItemStack corkedBottle = new(byEntity.World.GetBlock(CodeWithVariant("type", "corked"))) { Attributes = itemslot.Itemstack.Attributes };
                ItemStack cork = offhandSlot.Itemstack.Clone();
                cork.StackSize = 1;
                SetCork(corkedBottle, cork);
                offhandSlot.TakeOut(1);

                if (itemslot.StackSize == 1)
                {
                    itemslot.Itemstack = corkedBottle;
                }
                else
                {
                    itemslot.TakeOut(1);
                    if (!plr.InventoryManager.TryGiveItemstack(corkedBottle, true))
                    {
                        byEntity.World.SpawnItemEntity(corkedBottle, byEntity.Pos.AsBlockPos);
                    }
                }

                itemslot.MarkDirty();
                offhandSlot.MarkDirty();
                plr.InventoryManager.BroadcastHotbarSlot();

                PlayCorkingSound(api as ICoreServerAPI, corkedBottle, byEntity);

                handHandling = EnumHandHandling.PreventDefault;
                return;
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

        public override float GetContainingTransitionModifierContained(IWorldAccessor world, ItemSlot inSlot, EnumTransitionType transType)
        {
            return Attributes[transType == EnumTransitionType.Perish ? "perishRate" : "cureRate"].AsFloat(1);
        }

        public float SatMult => Attributes?["satMult"].AsFloat(1f) ?? 1f;

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
                            dsc.AppendLine(Lang.Get("efrecipes:- {0} {2} sat, {1} hp", Math.Round(props.Satiety * satLossMul * (liquidVolume / 10), 1), Math.Round(props.Health * healthLossMul * (liquidVolume / 10), 1), ItemExpandedFood.GetLocalizedFoodCategory(props.FoodCategory)));
                        }
                        else
                        {
                            dsc.AppendLine(Lang.Get("efrecipes:- {0} {1} sat", Math.Round(props.Satiety * satLossMul * (liquidVolume / 10), 1), ItemExpandedFood.GetLocalizedFoodCategory(props.FoodCategory)));
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
                    HotKeyCode = "ctrl",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => bs != null && GetCurrentLitres(inSlot.Itemstack) > 0,
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
                    HotKeyCode = "shift",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => true
                },

                new()
                {
                    ActionLangCode = "aculinaryartillery:heldhelp-uncork",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => bs == null && es == null && Variant["type"] == "corked"
                },
                new()
                {
                    ActionLangCode = "aculinaryartillery:heldhelp-cork",
                    HotKeyCode = "shift",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = corkStacks,
                    GetMatchingStacks = (wi, bs, es) => bs == null && es == null && Variant["type"] == "fired" ? wi.Itemstacks : null
                },
            ];
        }
    }

    /*************************************************************************************************************/
    public class BottleTextureSource : ITexPositionSource
    {
        public ItemStack? forContents;
        private readonly ICoreClientAPI capi;
        private TextureAtlasPosition? contentTextPos;
        private readonly CompositeTexture? contentTexture;
        private readonly TextureAtlasPosition blockTextPos;
        private readonly TextureAtlasPosition corkTextPos;

        public BottleTextureSource(ICoreClientAPI capi, ItemStack bottleStack, ItemStack? corkStack, CompositeTexture? contentTexture)
        {
            this.capi = capi;
            this.contentTexture = contentTexture;

            corkTextPos = corkStack != null ? capi.ItemTextureAtlas.GetPosition(corkStack.Item, "base") : capi.BlockTextureAtlas.GetPosition(bottleStack.Block, "map");
            blockTextPos = capi.BlockTextureAtlas.GetPosition(bottleStack.Block, "material");
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "map") return corkTextPos;
                if (textureCode == "material") return blockTextPos;

                if (contentTextPos == null && contentTexture != null)
                {
                    int textureSubId = ObjectCacheUtil.GetOrCreate(capi, "contenttexture-" + contentTexture.ToString() ?? "unknowncontent", () =>
                    {
                        capi.BlockTextureAtlas.GetOrInsertTexture(
                            contentTexture.Base.CopyWithPathPrefixAndAppendixOnce("textures/", ".png"),
                            out var id,
                            out _,
                            new CreateTextureDelegate(() =>
                            {
                                var bmp = capi.Assets.TryGet(contentTexture.Base.CopyWithPathPrefixAndAppendixOnce("textures/", ".png"))?.ToBitmap(capi);
                                if (bmp != null && contentTexture.Alpha != 255) bmp.MulAlpha(contentTexture.Alpha);
                                return bmp;
                            })
                        );
                        return id;
                    });

                    contentTextPos = capi.BlockTextureAtlas.Positions[textureSubId];
                }

                return contentTextPos ?? blockTextPos;
            }
        }
        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
    }
}
