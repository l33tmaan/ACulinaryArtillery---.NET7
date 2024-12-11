using Cairo;
using Cairo.Freetype;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Common;
using Vintagestory.GameContent;
using static System.Formats.Asn1.AsnWriter;

namespace ACulinaryArtillery
{
    public class ItemExpandedRawFood : Item, IExpandedFood, ITexPositionSource, IContainedMeshSource, IBakeableCallback, ICustomHandbookPageContent
    {
        public Size2i AtlasSize => targetAtlas.Size;
        public TextureAtlasPosition this[string textureCode] => GetOrCreateTexPos(GetTexturePath(textureCode));
        protected ITextureAtlasAPI targetAtlas;
        protected Shape nowTesselatingShape;

        public override bool Satisfies(ItemStack thisStack, ItemStack otherStack)
        {
            if (thisStack.Class == otherStack.Class && thisStack.Id == otherStack.Id)
            {
                if (!otherStack.Attributes.HasAttribute("madeWith") && thisStack.Attributes.HasAttribute("madeWith"))
                {
                    return true;
                }
            }
            return base.Satisfies(thisStack, otherStack);
        }
        public float SatMult
        {
            get { return Attributes?["satMult"].AsFloat(1f) ?? 1f; }
        }

        protected AssetLocation GetTexturePath(string textureCode)
        {
            AssetLocation texturePath = null;
            CompositeTexture tex;

            // Prio 1: Get from collectible textures
            if (Textures.TryGetValue(textureCode, out tex))
                texturePath = tex.Baked.BakedName;
            // Prio 2: Get from collectible textures, use "all" code
            else if (Textures.TryGetValue("all", out tex))
                texturePath = tex.Baked.BakedName;
            // Prio 3: Get from currently tesselating shape
            else
                nowTesselatingShape?.Textures.TryGetValue(textureCode, out texturePath);

            // Prio 4: The code is the path
            if (texturePath == null)
                texturePath = new AssetLocation(textureCode);

            return texturePath;
        }

        protected TextureAtlasPosition GetOrCreateTexPos(AssetLocation texturePath)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            TextureAtlasPosition texpos = targetAtlas[texturePath];

            if (texpos != null) return texpos;

            IAsset texAsset = capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
            bool success = targetAtlas.GetOrInsertTexture(texturePath, out _, out texpos, () => texAsset?.ToBitmap(capi));
            if (success) return texpos;

            texpos = targetAtlas.UnknownTexturePosition;
            capi.World.Logger.Warning("Item {0} defined texture {1}, but no such texture was found.", Code, texturePath);
            return texpos;
        }

        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, GridRecipe byRecipe)
        {
            base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
            ItemStack output = outputSlot.Itemstack;

            List<string> ingredients = new List<string>();
            float[] sat = new float[6];

            foreach (ItemSlot slot in allInputslots)
            {
                if (slot.Itemstack == null)
                    continue;

                CraftingRecipeIngredient match = null;
                if (byRecipe?.Ingredients != null)
                { foreach (var val in byRecipe.Ingredients) { if (val.Value.SatisfiesAsIngredient(slot.Itemstack)) { match = val.Value; break; } } }

                if (slot.Itemstack.Collectible is ItemExpandedRawFood)
                {
                    string[] addIngs = (slot.Itemstack.Attributes["madeWith"] as StringArrayAttribute)?.value;
                    float[] addSat = (slot.Itemstack.Attributes["expandedSats"] as FloatArrayAttribute)?.value;

                    if (addSat != null && addSat.Length == 6)
                        sat = sat.Zip(addSat, (x, y) => x + (y * match?.Quantity ?? 1)).ToArray();

                    if (addIngs != null && addIngs.Length > 0)
                    {
                        foreach (string aL in addIngs)
                        {
                            if (ingredients.Contains(aL))
                                continue;

                            ingredients.Add(aL);
                        }
                    }
                }
                else
                {
                    GetNutrientsFromIngredient(ref sat, slot.Itemstack.Collectible, match?.Quantity ?? 1);
                    string aL = slot.Itemstack.Collectible.Code.Domain + ":" + slot.Itemstack.Collectible.Code.Path;
                    if (ingredients.Contains(aL))
                        continue;

                    ingredients.Add(aL);
                }
            }

            ingredients.Sort();

            output.Attributes["madeWith"] = new StringArrayAttribute(ingredients.ToArray());
            output.Attributes["expandedSats"] = new FloatArrayAttribute(sat.ToArray());
        }

        public void OnCreatedByKneading(List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> input, ItemStack output)
        {
            List<string> ingredients = new List<string>();
            float[] sat = new float[6];

            foreach (var val in input)
            {
                if (val.Key.Itemstack.Collectible is ItemExpandedRawFood)
                {
                    string[] addIngs = (val.Key.Itemstack.Attributes["madeWith"] as StringArrayAttribute)?.value;
                    float[] addSat = (val.Key.Itemstack.Attributes["expandedSats"] as FloatArrayAttribute)?.value;
                    /*
                    if (addSat != null)
                    {
                        api.Logger.Debug("Prezip addSat values: " + string.Join("\n", addSat));
                        api.Logger.Debug("Quantity of item: " + val.Value.Quantity.ToString());
                    }
                    */
                    if (addSat != null && addSat.Length == 6)
                        sat = sat.Zip(addSat, (x, y) => x + (y * (val.Key.Itemstack.Collectible is ItemExpandedLiquid ? val.Value.Quantity / 10 : val.Value.Quantity))).ToArray();
                    //api.Logger.Debug(string.Join("\n", sat));
                    if (addIngs != null && addIngs.Length > 0)
                    {
                        foreach (string aL in addIngs)
                        {
                            if (ingredients.Contains(aL))
                                continue;

                            ingredients.Add(aL);
                        }
                    }
                }
                else
                {
                    GetNutrientsFromIngredient(ref sat, val.Key.Itemstack.Collectible, val.Value.Quantity);

                    string aL = val.Key.Itemstack.Collectible.Code.Domain + ":" + val.Key.Itemstack.Collectible.Code.Path;
                    if (ingredients.Contains(aL))
                        continue;

                    ingredients.Add(aL);
                }
            }

            ingredients.Sort();

            output.Attributes["madeWith"] = new StringArrayAttribute(ingredients.ToArray());
            output.Attributes["expandedSats"] = new FloatArrayAttribute(sat.ToArray());
        }

        public override bool CanSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemStack inputStack, ItemStack outputStack)
        {
            if (inputStack.Collectible.CombustibleProps == null)
                return false;
            if (outputStack == null)
                return true;

            if (!inputStack.Collectible.CombustibleProps.SmeltedStack.ResolvedItemstack.Equals(world, outputStack, GlobalConstants.IgnoredStackAttributes.Concat(new string[] { "madeWith", "expandedSats" }).ToArray()))
                return false;
            if (outputStack.StackSize >= outputStack.Collectible.MaxStackSize)
                return false;

            if (outputStack.Attributes["madeWith"] == null || !inputStack.Attributes["madeWith"].Equals(world, outputStack.Attributes["madeWith"]))
                return false;
            if (outputStack.Attributes["expandedSats"] == null || !inputStack.Attributes["expandedSats"].Equals(world, outputStack.Attributes["expandedSats"]))
                return false;

            return true;
        }

        public override void DoSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot, ItemSlot outputSlot)
        {
            if (!CanSmelt(world, cookingSlotsProvider, inputSlot.Itemstack, outputSlot.Itemstack))
                return;

            ItemStack smeltedStack = inputSlot.Itemstack.Collectible.CombustibleProps.SmeltedStack.ResolvedItemstack.Clone();

            // Copy over spoilage values but reduce them by a bit
            TransitionState state = UpdateAndGetTransitionState(world, new DummySlot(inputSlot.Itemstack), EnumTransitionType.Perish);

            if (state != null)
            {
                TransitionState smeltedState = smeltedStack.Collectible.UpdateAndGetTransitionState(world, new DummySlot(smeltedStack), EnumTransitionType.Perish);

                float nowTransitionedHours = (state.TransitionedHours / (state.TransitionHours + state.FreshHours)) * 0.8f * (smeltedState.TransitionHours + smeltedState.FreshHours) - 1;

                smeltedStack.Collectible.SetTransitionState(smeltedStack, EnumTransitionType.Perish, Math.Max(0, nowTransitionedHours));
            }

            int batchSize = 1;

            string[] ingredients = (inputSlot.Itemstack.Attributes["madeWith"] as StringArrayAttribute)?.value;
            float[] satieties = (inputSlot.Itemstack.Attributes["expandedSats"] as FloatArrayAttribute)?.value;


            if (ingredients != null)
                smeltedStack.Attributes["madeWith"] = new StringArrayAttribute(ingredients);
            if (satieties != null)
                smeltedStack.Attributes["expandedSats"] = new FloatArrayAttribute(satieties);

            if (outputSlot.Itemstack == null)
            {
                outputSlot.Itemstack = smeltedStack;
                outputSlot.Itemstack.StackSize = batchSize * smeltedStack.StackSize;
            }
            else
            {
                smeltedStack.StackSize = batchSize * smeltedStack.StackSize;

                // use TryMergeStacks to average spoilage rate and temperature
                ItemStackMergeOperation op = new ItemStackMergeOperation(world, EnumMouseButton.Left, 0, EnumMergePriority.ConfirmedMerge, batchSize * smeltedStack.StackSize);
                op.SourceSlot = new DummySlot(smeltedStack);
                op.SinkSlot = new DummySlot(outputSlot.Itemstack);
                outputSlot.Itemstack.Collectible.TryMergeStacks(op);
                outputSlot.Itemstack = op.SinkSlot.Itemstack;
            }

            inputSlot.Itemstack.StackSize -= batchSize * CombustibleProps.SmeltedRatio;

            if (inputSlot.Itemstack.StackSize <= 0)
            {
                inputSlot.Itemstack = null;
            }

            outputSlot.MarkDirty();
        }

        public override ItemStack OnTransitionNow(ItemSlot slot, TransitionableProperties props)
        {
            string[] ings = (slot.Itemstack.Attributes["madeWith"] as StringArrayAttribute)?.value;
            float[] xNutr = (slot.Itemstack.Attributes["expandedSats"] as FloatArrayAttribute)?.value;

            ItemStack org = base.OnTransitionNow(slot, props);
            if (org == null || !(org.Collectible is ItemExpandedRawFood))
                return org;
            if (ings != null)
                org.Attributes["madeWith"] = new StringArrayAttribute(ings);
            if (xNutr != null && xNutr.Length > 0)
                org.Attributes["expandedSats"] = new FloatArrayAttribute(xNutr);
            return org;
        }

        public void GetNutrientsFromIngredient(ref float[] satHolder, CollectibleObject ing, int mult)
        {
            TreeAttribute check = Attributes?["expandedNutritionProps"].ToAttribute() as TreeAttribute;
            List<string> chk = new List<string>();
            if (check != null)
                foreach (var val in check)
                    chk.Add(val.Key);

            FoodNutritionProperties ingProps = null;
            if (chk.Count > 0)
                ingProps = Attributes["expandedNutritionProps"][FindMatch(ing.Code.Domain + ":" + ing.Code.Path, chk.ToArray())].AsObject<FoodNutritionProperties>();
            if (ingProps == null)
                ingProps = ing.Attributes?["nutritionPropsWhenInMeal"].AsObject<FoodNutritionProperties>();
            if (ingProps == null)
                ingProps = ing.NutritionProps;
            if (ingProps == null)
                return;

            if (ingProps.Health != 0)
                satHolder[(int)EnumNutritionMatch.Hp] += ingProps.Health * mult;

            switch (ingProps.FoodCategory)
            {
                case EnumFoodCategory.Fruit:
                    satHolder[(int)EnumNutritionMatch.Fruit] += ingProps.Satiety * mult;
                    break;

                case EnumFoodCategory.Grain:
                    satHolder[(int)EnumNutritionMatch.Grain] += ingProps.Satiety * mult;
                    break;

                case EnumFoodCategory.Vegetable:
                    satHolder[(int)EnumNutritionMatch.Vegetable] += ingProps.Satiety * mult;
                    break;

                case EnumFoodCategory.Protein:
                    satHolder[(int)EnumNutritionMatch.Protein] += ingProps.Satiety * mult;
                    break;

                case EnumFoodCategory.Dairy:
                    satHolder[(int)EnumNutritionMatch.Dairy] += ingProps.Satiety * mult;
                    break;
            }
        }

        public void ListIngredients(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)

        {
            string desc = Lang.Get("efrecipes:Made with ");
            string[] ings = (inSlot.Itemstack.Attributes["madeWith"] as StringArrayAttribute)?.value;

            if (ings == null || ings.Length < 1)
            {
                return;
            }

            List<string> readable = new List<string>();
            for (int i = 0; i < ings.Length; i++)
            {
                AssetLocation obj = new AssetLocation(ings[i]);
                Block block = world.GetBlock(obj);
                string ingInfo = Lang.GetIfExists("recipeingredient-" + (block != null ? "block-" : "item-") + obj.Path);
                if (ingInfo != null && !readable.Contains(ingInfo))
                    readable.Add(ingInfo);
            }

            ings = readable.ToArray();

            if (ings == null || ings.Length < 1)
            {
                return;
            }


            if (ings.Length < 2)
            {
                desc += ings[0];

                dsc.AppendLine(desc);
                return;
            }

            for (int i = 0; i < ings.Length; i++)
            {
                AssetLocation obj = new AssetLocation(ings[i]);
                Block block = world.GetBlock(obj);

                if (i + 1 == ings.Length)
                {
                    desc += Lang.Get("efrecipes:and ") + ings[i];
                }
                else
                {
                    desc += ings[i] + ", ";
                }
            }

            dsc.AppendLine(desc);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            ListIngredients(inSlot, dsc, world, withDebugInfo);
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            RenderAlphaTest = 0.5f;
            string[] ings = (itemstack.Attributes?["madeWith"] as StringArrayAttribute)?.value;
            if (ings == null || ings.Length <= 0)
                return;

            Dictionary<string, MultiTextureMeshRef> meshrefs = ObjectCacheUtil.GetOrCreate(capi, "expandedFoodGuiMeshRefs", () =>
            {
                return new Dictionary<string, MultiTextureMeshRef>();
            });

            string key = Code.ToShortString() + string.Join("|", ings);
            MultiTextureMeshRef meshref;
            if (!meshrefs.TryGetValue(key, out meshref))
            {
                MeshData mesh = GenMesh(capi.ItemTextureAtlas, ings, new Vec3f(0, 0, 0));
                if (mesh == null)
                    return;

                meshrefs[key] = meshref = capi.Render.UploadMultiTextureMesh(mesh);
            }

            if (meshref != null)
                renderinfo.ModelRef = meshref;

        }

        public virtual MeshData GenMesh(ITextureAtlasAPI targetAtlas, string[] ings, Vec3f rot = null, ITesselatorAPI tesselator = null)
        {
            this.targetAtlas = targetAtlas;
            nowTesselatingShape = null;
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (tesselator == null)
                tesselator = capi.Tesselator;

            List<AssetLocation> addShapes = new List<AssetLocation>();

            TreeAttribute check = Attributes?["renderIngredients"].ToAttribute() as TreeAttribute;
            List<string> chk = new List<string>();
            if (check != null)
                foreach (var val in check)
                    chk.Add(val.Key);
            else
                return null;
            List<Dictionary<String, String>> texureMappingsPerShape = new List<Dictionary<string, string>>();

            for (int i = 0; i < ings.Length; i++)
            {
                Dictionary<String, String> textureMap = new Dictionary<String, String>();
                string path = null;
                String match = FindMatch(ings[i], chk.ToArray());
                var value = Attributes?["renderIngredients"]?[match];
                if (value is not null && !((value.ToAttribute() as TreeAttribute) is null))
                {
                    String wildCard = WildcardUtil.GetWildcardValue(new AssetLocation(match), new AssetLocation(ings[i]));
                    String name;
                    //String itemForLog = Code.ToString();
                    //String ingredientForLog = ings[i].ToString();
                    TreeAttribute keyValuePairs = value.ToAttribute() as TreeAttribute;
                    path = keyValuePairs.GetAsString("shape");
                    name = keyValuePairs.GetAsString("name");
                    TreeAttribute textureMappings = keyValuePairs.GetTreeAttribute("textureMap") as TreeAttribute;
                    foreach (var key in textureMappings?.Keys)
                    {
                        String replacedString = textureMappings.GetString(key).Replace("{" + name + "}", wildCard);
                        textureMap[key] = replacedString;

                    }
                }
                else
                {
                    path = Attributes?["renderIngredients"]?[FindMatch(ings[i], chk.ToArray())]?.AsString();
                }


                if (path == null)
                    continue;
                AssetLocation shape = new AssetLocation(path);
                if (shape != null)
                {
                    addShapes.Add(shape);
                    texureMappingsPerShape.Add(textureMap);
                }

            }

            if (addShapes.Count <= 0)
                return null;

            // Render first added ingredient before everything else to avoid transparent bread
            AssetLocation baseIngredient = addShapes.Last();
            Dictionary<String, String> baseMapping = texureMappingsPerShape.Last();
            //texureMappingsPerShape.Remove(baseMapping);
            //texureMappingsPerShape.Insert(0, baseMapping);
            //addShapes.Remove(baseIngredient);
            //addShapes.Insert(0, baseIngredient);

            MeshData mesh = null;
            float uvoffset = 0;
            for (int i = 0; i < addShapes.Count; i++)
            {
                MeshData addIng;
                Shape addShape;

                if (!addShapes[i].Valid || (addShape = capi.Assets.TryGet(addShapes[i]).ToObject<Shape>()) == null)
                    continue;


                var keys = (addShape.Textures?.Keys);
                Shape clonedAddShape = addShape.Clone();
                if(addShape.Textures != null)
                {
                    clonedAddShape.Textures = new Dictionary<string, AssetLocation>(addShape.Textures);
                }
                
                //clonedAddShape.Textures.Clear();
                if (keys is not null && texureMappingsPerShape[i].Count() > 0)
                {

                    foreach (var key in texureMappingsPerShape[i].Keys)
                    {
                        AssetLocation ass = GetTexturePath(texureMappingsPerShape[i][key]); // path to desired texture
                        if (clonedAddShape.Textures.ContainsKey(key))
                        {
                            clonedAddShape.Textures[key] = ass;
                        }
                        else
                        {
                            clonedAddShape.Textures[key] = GetTexturePath(key);
                        }

                    }

                    ShapeTextureSource textureSource = new ShapeTextureSource(capi, clonedAddShape, null);

                    if (addShapes.Where(x => x != null && x == addShapes[i]).Count() > 1)
                    {
                        int texHeight = clonedAddShape.TextureHeight;
                        int texWidth = clonedAddShape.TextureWidth;
                        foreach (ShapeElement elm in clonedAddShape.Elements)
                        {
                            foreach (ShapeElementFace face in elm.FacesResolved)
                            {
                                if(face != null)
                                {
                                    float faceWidth = Math.Abs(face.Uv[2] - face.Uv[0]);
                                    float faceHeight = Math.Abs(face.Uv[3] - face.Uv[1]);
                                    float ustart = (float)Math.Floor(uvoffset * (texWidth - faceWidth));
                                    float vstart = (float)Math.Floor(uvoffset * (texHeight - faceHeight));
                                    face.Uv[0] = ustart;
                                    face.Uv[1] = vstart;
                                    face.Uv[2] = ustart + faceWidth;
                                    face.Uv[3] = vstart + faceHeight;
                                }
                            }
                        }
                        uvoffset += 0.0625f;
                    }

                    tesselator.TesselateShape("ACA", clonedAddShape, out addIng, textureSource, rot);
                }
                else
                {
                    tesselator.TesselateShape("ACA", clonedAddShape, out addIng, this, rot);
                }

                if (mesh == null)
                {
                    mesh = addIng;

                }
                else
                    mesh.AddMeshData(addIng);
            }

            mesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.BlendNoCull);
            return mesh;
        }


        public virtual MeshData GenMesh(ItemStack stack, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos = null)
        {
            this.targetAtlas = targetAtlas;
            nowTesselatingShape = null;
            ICoreClientAPI capi = api as ICoreClientAPI;
            var be = api.World.BlockAccessor.GetBlockEntity(atBlockPos);

            string[] ings = (stack.Attributes?["madeWith"] as StringArrayAttribute)?.value;
            if (ings == null || ings.Length <= 0)
            {
                if (stack.Item?.Shape?.Base != null)
                    nowTesselatingShape = capi.TesselatorManager.GetCachedShape(stack.Item.Shape.Base);
                capi.Tesselator.TesselateItem(stack.Item, out MeshData mesh, this);
                mesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.BlendNoCull);
                return mesh;
            }

            return GenMesh(targetAtlas, ings, new Vec3f(0, be.Block.Shape.rotateY, 0), capi.Tesselator);
        }

        public string GetMeshCacheKey(ItemStack stack)
        {
            string[] ings = (stack.Attributes?["madeWith"] as StringArrayAttribute)?.value;
            if (ings == null) return Code.ToShortString();

            return Code.ToShortString() + string.Join(",", ings);
        }

        public MeshData GenMesh(ICoreClientAPI capi, string[] ings, ItemStack stack, Vec3f rot = null, ITesselatorAPI tesselator = null)
        {
            if (tesselator == null)
                tesselator = capi.Tesselator;

            List<AssetLocation> addShapes = new List<AssetLocation>();

            TreeAttribute check = Attributes?["renderIngredients"].ToAttribute() as TreeAttribute;
            List<string> chk = new List<string>();
            if (check != null)
                foreach (var val in check)
                    chk.Add(val.Key);
            else
                return null;

            for (int i = 0; i < ings.Length; i++)
            {
                string path = null;
                path = Attributes?["renderIngredients"]?[FindMatch(ings[i], chk.ToArray())]?.AsString();
                if (path == null)
                    continue;
                AssetLocation shape = new AssetLocation(path);
                if (shape != null)
                    addShapes.Add(shape);
            }

            if (addShapes.Count <= 0)
                return new MeshData();

            MeshData mesh = null;
            MeshData addIng;

            for (int i = 0; i < addShapes.Count; i++)
            {
                Shape addShape;
                if (!addShapes[i].Valid || (addShape = capi.Assets.TryGet(addShapes[i]).ToObject<Shape>()) == null)
                    continue;

                if (addShape.Textures != null)
                {
                    var tt = addShape.Textures.Keys.First();
                    CompositeTexture tex2;
                    stack.Item.Textures.TryGetValue(tt, out tex2);
                    var contentSource = new EFTextureSource(capi, stack, tex2);
                    tesselator.TesselateShape("expandedfood", addShape, out addIng, contentSource, rot);
                    if (mesh == null)
                        mesh = addIng;
                    else
                        mesh.AddMeshData(addIng);
                }
            }

            return mesh;
        }

        public FoodNutritionProperties[] GetPropsFromArray(float[] satieties)
        {
            if (satieties == null || satieties.Length < 6)
                return null;

            List<FoodNutritionProperties> props = new List<FoodNutritionProperties>();

            if (satieties[(int)EnumNutritionMatch.Fruit] != 0)
                props.Add(new FoodNutritionProperties() { FoodCategory = EnumFoodCategory.Fruit, Satiety = satieties[(int)EnumNutritionMatch.Fruit] * SatMult });
            if (satieties[(int)EnumNutritionMatch.Grain] != 0)
                props.Add(new FoodNutritionProperties() { FoodCategory = EnumFoodCategory.Grain, Satiety = satieties[(int)EnumNutritionMatch.Grain] * SatMult });
            if (satieties[(int)EnumNutritionMatch.Vegetable] != 0)
                props.Add(new FoodNutritionProperties() { FoodCategory = EnumFoodCategory.Vegetable, Satiety = satieties[(int)EnumNutritionMatch.Vegetable] * SatMult });
            if (satieties[(int)EnumNutritionMatch.Protein] != 0)
                props.Add(new FoodNutritionProperties() { FoodCategory = EnumFoodCategory.Protein, Satiety = satieties[(int)EnumNutritionMatch.Protein] * SatMult });
            if (satieties[(int)EnumNutritionMatch.Dairy] != 0)
                props.Add(new FoodNutritionProperties() { FoodCategory = EnumFoodCategory.Dairy, Satiety = satieties[(int)EnumNutritionMatch.Dairy] * SatMult });

            if (satieties[0] != 0 && props.Count > 0)
                props[0].Health = satieties[0] * SatMult;

            return props.ToArray();
        }

        public string FindMatch(string needle, string[] haystack)
        {
            if (needle == null || haystack == null || haystack.Length <= 0)
                return "";

            foreach (string hay in haystack)
            {
                if (hay == needle || WildcardUtil.Match(hay, needle))
                    return hay;
            }

            return "";
        }

        public virtual RichTextComponentBase[] GetHandbookInfo(ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            ItemStack stack = inSlot.Itemstack;

            List<RichTextComponentBase> components = new List<RichTextComponentBase>();

            components.Add(new ItemstackTextComponent(capi, stack, 100, 10, EnumFloat.Left));
            components.Add(new RichTextComponent(capi, stack.GetName() + "\n", CairoFont.WhiteSmallishText()));

            components.AddRange(VtmlUtil.Richtextify(capi, stack.GetDescription(capi.World, inSlot), CairoFont.WhiteSmallText()));



            components.Add(new ClearFloatTextComponent(capi, 10));


            List<ItemStack> breakBlocks = new List<ItemStack>();

            //Dictionary<AssetLocation, ItemStack> breakBlocks = new Dictionary<AssetLocation, ItemStack>();

            foreach (var blockStack in allStacks)
            {
                if (blockStack.Block == null)
                    continue;

                BlockDropItemStack[] droppedStacks = blockStack.Block.GetDropsForHandbook(blockStack, capi.World.Player);
                if (droppedStacks == null)
                    continue;

                for (int i = 0; i < droppedStacks.Length; i++)
                {
                    ItemStack droppedStack = droppedStacks[i].ResolvedItemstack;

                    if (droppedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                    {
                        //breakBlocks[val.Block.Code] = new ItemStack(val.Block); - why val.Block? It breaks lantern textures
                        breakBlocks.Add(blockStack);
                        //breakBlocks[blockStack.Block.Code] = droppedStack;
                    }
                }
            }




            if (stack.Class == EnumItemClass.Block)
            {
                BlockDropItemStack[] blockdropStacks = stack.Block.GetDropsForHandbook(stack, capi.World.Player);
                List<ItemStack> dropsStacks = new List<ItemStack>();
                foreach (var val in blockdropStacks)
                {
                    dropsStacks.Add(val.ResolvedItemstack);
                }

                if (dropsStacks != null && dropsStacks.Count > 0)
                {
                    if (dropsStacks.Count == 1 && breakBlocks.Count == 1 && breakBlocks[0].Equals(capi.World, dropsStacks[0], GlobalConstants.IgnoredStackAttributes))
                    {
                        // No need to display the same info twice
                    }
                    else
                    {
                        components.Add(new RichTextComponent(capi, Lang.Get("Drops when broken") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                        while (dropsStacks.Count > 0)
                        {
                            ItemStack dstack = dropsStacks[0];
                            dropsStacks.RemoveAt(0);
                            if (dstack == null)
                                continue;

                            SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, dropsStacks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                            components.Add(comp);
                        }

                        components.Add(new ClearFloatTextComponent(capi, 10));
                    }
                }
            }



            // Obtained through...
            // * Killing drifters
            // * From flax crops
            List<string> killCreatures = new List<string>();

            foreach (var val in capi.World.EntityTypes)
            {
                if (val.Drops == null)
                    continue;

                for (int i = 0; i < val.Drops.Length; i++)
                {
                    if (val.Drops[i].ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                    {
                        killCreatures.Add(Lang.Get(val.Code.Domain + ":item-creature-" + val.Code.Path));
                    }
                }
            }


            bool haveText = false;

            if (killCreatures.Count > 0)
            {
                components.Add(new RichTextComponent(capi, Lang.Get("Obtained by killing") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                components.Add(new RichTextComponent(capi, string.Join(", ", killCreatures) + "\n", CairoFont.WhiteSmallText()));
                haveText = true;
            }



            if (breakBlocks.Count > 0)
            {
                components.Add(new RichTextComponent(capi, (haveText ? "\n" : "") + Lang.Get("Obtained by breaking") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));

                while (breakBlocks.Count > 0)
                {
                    ItemStack dstack = breakBlocks[0];
                    breakBlocks.RemoveAt(0);
                    if (dstack == null)
                        continue;

                    SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, breakBlocks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                    components.Add(comp);
                }

                haveText = true;
            }


            // Found in....
            string customFoundIn = stack.Collectible.Attributes?["handbook"]?["foundIn"]?.AsString(null);
            if (customFoundIn != null)
            {
                components.Add(new RichTextComponent(capi, (haveText ? "\n" : "") + Lang.Get("Found in") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                components.Add(new RichTextComponent(capi, Lang.Get(customFoundIn), CairoFont.WhiteSmallText()));
                haveText = true;
            }


            if (Attributes?["hostRockFor"].Exists == true)
            {
                ushort[] blockids = Attributes?["hostRockFor"].AsArray<ushort>();

                OrderedDictionary<string, List<ItemStack>> blocks = new OrderedDictionary<string, List<ItemStack>>();

                for (int i = 0; i < blockids.Length; i++)
                {
                    Block block = api.World.Blocks[blockids[i]];

                    string key = block.Code.ToString();
                    if (block.Attributes?["handbook"]["groupBy"].Exists == true)
                    {
                        key = block.Attributes["handbook"]["groupBy"].AsArray<string>()[0];
                    }

                    if (!blocks.ContainsKey(key))
                    {
                        blocks[key] = new List<ItemStack>();
                    }

                    blocks[key].Add(new ItemStack(block));
                }

                components.Add(new RichTextComponent(capi, (haveText ? "\n" : "") + Lang.Get("Host rock for") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));

                foreach (var val in blocks)
                {
                    components.Add(new SlideshowItemstackTextComponent(capi, val.Value.ToArray(), 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                }

                haveText = true;
            }


            if (Attributes?["hostRock"].Exists == true)
            {
                ushort[] blockids = Attributes?["hostRock"].AsArray<ushort>();

                OrderedDictionary<string, List<ItemStack>> blocks = new OrderedDictionary<string, List<ItemStack>>();

                for (int i = 0; i < blockids.Length; i++)
                {
                    Block block = api.World.Blocks[blockids[i]];

                    string key = block.Code.ToString();
                    if (block.Attributes?["handbook"]["groupBy"].Exists == true)
                    {
                        key = block.Attributes["handbook"]["groupBy"].AsArray<string>()[0];
                    }

                    if (!blocks.ContainsKey(key))
                    {
                        blocks[key] = new List<ItemStack>();
                    }

                    blocks[key].Add(new ItemStack(block));
                }

                components.Add(new RichTextComponent(capi, (haveText ? "\n" : "") + Lang.Get("Occurs in host rock") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));

                foreach (var val in blocks)
                {
                    components.Add(new SlideshowItemstackTextComponent(capi, val.Value.ToArray(), 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                }

                haveText = true;
            }



            // Alloy for...


            Dictionary<AssetLocation, ItemStack> alloyables = new Dictionary<AssetLocation, ItemStack>();
            foreach (var val in capi.GetMetalAlloys())
            {
                foreach (var ing in val.Ingredients)
                {
                    if (ing.ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                    {
                        alloyables[val.Output.ResolvedItemstack.Collectible.Code] = val.Output.ResolvedItemstack;
                    }
                }
            }

            if (alloyables.Count > 0)
            {
                components.Add(new RichTextComponent(capi, (haveText ? "\n" : "") + Lang.Get("Alloy for") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                foreach (var val in alloyables)
                {
                    components.Add(new ItemstackTextComponent(capi, val.Value, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                }

                haveText = true;
            }

            // Smelts into
            if (CombustibleProps?.SmeltedStack?.ResolvedItemstack != null && !CombustibleProps.SmeltedStack.ResolvedItemstack.Equals(api.World, stack, GlobalConstants.IgnoredStackAttributes))
            {
                string smelttype = CombustibleProps.SmeltingType.ToString().ToLowerInvariant();
                string title = Lang.Get("game:smeltdesc-" + smelttype + "-title");


                components.Add(new RichTextComponent(capi, (haveText ? "\n" : "") + title + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                components.Add(new ItemstackTextComponent(capi, CombustibleProps.SmeltedStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                haveText = true;
            }

            // Pulverizes into
            if (CrushingProps?.CrushedStack?.ResolvedItemstack != null && !CrushingProps.CrushedStack.ResolvedItemstack.Equals(api.World, stack, GlobalConstants.IgnoredStackAttributes))
            {
                string title = Lang.Get("game:pulverizesdesc-title");

                components.Add(new RichTextComponent(capi, (haveText ? "\n" : "") + title + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                components.Add(new ItemstackTextComponent(capi, CrushingProps.CrushedStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                haveText = true;
            }


            // Grinds into
            if (GrindingProps?.GroundStack?.ResolvedItemstack != null && !GrindingProps.GroundStack.ResolvedItemstack.Equals(api.World, stack, GlobalConstants.IgnoredStackAttributes))
            {
                components.Add(new RichTextComponent(capi, (haveText ? "\n" : "") + Lang.Get("Grinds into") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                components.Add(new ItemstackTextComponent(capi, GrindingProps.GroundStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                haveText = true;
            }

            TransitionableProperties[] props = GetTransitionableProperties(api.World, stack, null);

            foreach (var prop in props)
            {
                switch (prop.Type)
                {
                    case EnumTransitionType.Cure:
                        components.Add(new RichTextComponent(capi, (haveText ? "\n" : "") + Lang.Get("After {0} hours, cures into", prop.TransitionHours.avg) + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                        components.Add(new ItemstackTextComponent(capi, prop.TransitionedStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                        break;

                    case EnumTransitionType.Ripen:
                        components.Add(new RichTextComponent(capi, (haveText ? "\n" : "") + Lang.Get("After {0} hours of open storage, ripens into", prop.TransitionHours.avg) + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                        components.Add(new ItemstackTextComponent(capi, prop.TransitionedStack.ResolvedItemstack, 40, 10, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                        break;

                    case EnumTransitionType.Dry:
                        break;

                    case EnumTransitionType.Convert:
                        break;

                    case EnumTransitionType.Perish:
                        break;

                }
            }


            // Alloyable from

            Dictionary<AssetLocation, MetalAlloyIngredient[]> alloyableFrom = new Dictionary<AssetLocation, MetalAlloyIngredient[]>();
            foreach (var val in capi.GetMetalAlloys())
            {
                if (val.Output.ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    List<MetalAlloyIngredient> ingreds = new List<MetalAlloyIngredient>();
                    foreach (var ing in val.Ingredients)
                        ingreds.Add(ing);
                    alloyableFrom[val.Output.ResolvedItemstack.Collectible.Code] = ingreds.ToArray();
                }
            }

            if (alloyableFrom.Count > 0)
            {
                components.Add(new RichTextComponent(capi, (haveText ? "\n" : "") + Lang.Get("Alloyed from") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                foreach (var val in alloyableFrom)
                {
                    foreach (var ingred in val.Value)
                    {
                        string ratio = " " + Lang.Get("alloy-ratio-from-to", (int)(ingred.MinRatio * 100), (int)(ingred.MaxRatio * 100));
                        components.Add(new RichTextComponent(capi, ratio, CairoFont.WhiteSmallText()));
                        ItemstackComponentBase comp = new ItemstackTextComponent(capi, ingred.ResolvedItemstack, 30, 5, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        comp.offY = 8;
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }

                components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));

                haveText = true;
            }

            // Ingredient for...
            // Pickaxe
            // Axe
            ItemStack maxstack = stack.Clone();
            maxstack.StackSize = maxstack.Collectible.MaxStackSize; // because SatisfiesAsIngredient() tests for stacksize

            List<ItemStack> recipestacks = new List<ItemStack>();

            // COMMENT THIS OUT
            foreach (var recval in capi.World.GridRecipes)
            {
                foreach (var val in recval.resolvedIngredients)
                {
                    CraftingRecipeIngredient ingred = val;

                    if (ingred != null && ingred.SatisfiesAsIngredient(maxstack) && !recipestacks.Any(s => s.Equals(capi.World, recval.Output.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes)))
                    {
                        ItemStack outstack = recval.Output.ResolvedItemstack;
                        DummySlot outSlot = new DummySlot(outstack);

                        DummySlot[] inSlots = new DummySlot[recval.Width * recval.Height];
                        for (int x = 0; x < recval.Width; x++)
                        {
                            for (int y = 0; y < recval.Height; y++)
                            {
                                CraftingRecipeIngredient inIngred = recval.GetElementInGrid(y, x, recval.resolvedIngredients, recval.Width);
                                ItemStack ingredStack = inIngred?.ResolvedItemstack?.Clone();
                                if (inIngred == val)
                                    ingredStack = maxstack;

                                inSlots[y * recval.Width + x] = new DummySlot(ingredStack);
                            }
                        }


                        outstack.Collectible.OnCreatedByCrafting(inSlots, outSlot, recval);
                        recipestacks.Add(outSlot.Itemstack);
                    }
                }

            }

            foreach (var recval in capi.World.GridRecipes)
            {
                foreach (var val in recval.resolvedIngredients)
                {
                    CraftingRecipeIngredient ingred = val;

                    if (ingred != null && ingred.SatisfiesAsIngredient(maxstack) && !recipestacks.Any(s => s.Equals(capi.World, recval.Output.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes)))
                    {
                        ItemStack outstack = recval.Output.ResolvedItemstack;
                        DummySlot outSlot = new DummySlot(outstack);

                        DummySlot[] inSlots = new DummySlot[recval.Width * recval.Height];
                        for (int x = 0; x < recval.Width; x++)
                        {
                            for (int y = 0; y < recval.Height; y++)
                            {
                                CraftingRecipeIngredient inIngred = recval.GetElementInGrid(y, x, recval.resolvedIngredients, recval.Width);
                                ItemStack ingredStack = inIngred?.ResolvedItemstack?.Clone();
                                if (inIngred == val)
                                    ingredStack = maxstack;

                                inSlots[y * recval.Width + x] = new DummySlot(ingredStack);
                            }
                        }


                        outstack.Collectible.OnCreatedByCrafting(inSlots, outSlot, recval);
                        recipestacks.Add(outSlot.Itemstack);
                    }
                }

            }

            foreach (var val in capi.GetSmithingRecipes())
            {
                if (val.Ingredient.SatisfiesAsIngredient(maxstack) && !recipestacks.Any(s => s.Equals(capi.World, val.Output.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes)))
                {
                    recipestacks.Add(val.Output.ResolvedItemstack);
                }
            }


            foreach (var val in capi.GetClayformingRecipes())
            {
                if (val.Ingredient.SatisfiesAsIngredient(maxstack) && !recipestacks.Any(s => s.Equals(capi.World, val.Output.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes)))
                {
                    recipestacks.Add(val.Output.ResolvedItemstack);
                }
            }


            foreach (var val in capi.GetKnappingRecipes())
            {
                if (val.Ingredient.SatisfiesAsIngredient(maxstack) && !recipestacks.Any(s => s.Equals(capi.World, val.Output.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes)))
                {
                    recipestacks.Add(val.Output.ResolvedItemstack);
                }
            }


            foreach (var recipe in capi.GetBarrelRecipes())
            {
                foreach (var ingred in recipe.Ingredients)
                {
                    if (ingred.SatisfiesAsIngredient(maxstack) && !recipestacks.Any(s => s.Equals(capi.World, recipe.Output.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes)))
                    {
                        recipestacks.Add(recipe.Output.ResolvedItemstack);
                    }
                }
            }



            if (recipestacks.Count > 0)
            {
                components.Add(new ClearFloatTextComponent(capi, 10));
                components.Add(new RichTextComponent(capi, Lang.Get("Ingredient for") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));

                while (recipestacks.Count > 0)
                {
                    ItemStack dstack = recipestacks[0];
                    recipestacks.RemoveAt(0);
                    if (dstack == null)
                        continue;

                    SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, recipestacks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                    components.Add(comp);
                }
            }




            // Created by....
            // * Smithing
            // * Grid crafting:
            //   x x x
            //   x x x
            //   x x x

            bool smithable = false;
            bool knappable = false;
            bool clayformable = false;

            foreach (var val in capi.GetSmithingRecipes())
            {
                if (val.Output.ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    smithable = true;
                    break;
                }
            }

            foreach (var val in capi.GetKnappingRecipes())
            {
                if (val.Output.ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    knappable = true;
                    break;
                }
            }


            foreach (var val in capi.GetClayformingRecipes())
            {
                if (val.Output.ResolvedItemstack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    clayformable = true;
                    break;
                }
            }


            List<GridRecipe> grecipes = new List<GridRecipe>();

            foreach (var val in capi.World.GridRecipes)
            {
                if (val.Output.ResolvedItemstack.Satisfies(stack))
                {
                    grecipes.Add(val);
                }
            }


            List<ItemStack> bakables = new List<ItemStack>();
            List<ItemStack> grindables = new List<ItemStack>();
            List<ItemStack> crushables = new List<ItemStack>();
            List<ItemStack> curables = new List<ItemStack>();
            List<ItemStack> ripenables = new List<ItemStack>();

            foreach (var val in allStacks)
            {
                ItemStack smeltedStack = val.Collectible.CombustibleProps?.SmeltedStack?.ResolvedItemstack;
                if (smeltedStack != null && smeltedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !bakables.Any(s => s.Equals(capi.World, smeltedStack, GlobalConstants.IgnoredStackAttributes)))
                {
                    bakables.Add(val);
                }

                ItemStack groundStack = val.Collectible.GrindingProps?.GroundStack.ResolvedItemstack;
                if (groundStack != null && groundStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !grindables.Any(s => s.Equals(capi.World, groundStack, GlobalConstants.IgnoredStackAttributes)))
                {
                    grindables.Add(val);
                }

                ItemStack crushedStack = val.Collectible.CrushingProps?.CrushedStack.ResolvedItemstack;
                if (crushedStack != null && crushedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !crushables.Any(s => s.Equals(capi.World, crushedStack, GlobalConstants.IgnoredStackAttributes)))
                {
                    crushables.Add(val);
                }

                TransitionableProperties[] oprops = val.Collectible.GetTransitionableProperties(api.World, val, null);
                if (oprops != null)
                {
                    foreach (var prop in oprops)
                    {
                        ItemStack transitionedStack = prop.TransitionedStack?.ResolvedItemstack;

                        switch (prop.Type)
                        {
                            case EnumTransitionType.Cure:
                                if (transitionedStack != null && transitionedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !curables.Any(s => s.Equals(capi.World, transitionedStack, GlobalConstants.IgnoredStackAttributes)))
                                {
                                    curables.Add(val);
                                }
                                break;

                            case EnumTransitionType.Ripen:
                                if (transitionedStack != null && transitionedStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes) && !curables.Any(s => s.Equals(capi.World, transitionedStack, GlobalConstants.IgnoredStackAttributes)))
                                {
                                    ripenables.Add(val);
                                }
                                break;


                            case EnumTransitionType.Dry:
                                break;

                            case EnumTransitionType.Convert:
                                break;

                            case EnumTransitionType.Perish:
                                break;

                        }
                    }
                }

            }


            List<RichTextComponentBase> barrelRecipestext = new List<RichTextComponentBase>();
            Dictionary<string, List<BarrelRecipe>> brecipesbyName = new Dictionary<string, List<BarrelRecipe>>();
            foreach (var recipe in capi.GetBarrelRecipes())
            {
                ItemStack mixdStack = recipe.Output.ResolvedItemstack;

                if (mixdStack != null && mixdStack.Equals(capi.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    List<BarrelRecipe> tmp;

                    if (!brecipesbyName.TryGetValue(recipe.Code, out tmp))
                    {
                        brecipesbyName[recipe.Code] = tmp = new List<BarrelRecipe>();
                    }

                    tmp.Add(recipe);
                }
            }



            foreach (var recipes in brecipesbyName.Values)
            {
                int ingredientsLen = recipes[0].Ingredients.Length;
                ItemStack[][] ingstacks = new ItemStack[ingredientsLen][];

                for (int i = 0; i < recipes.Count; i++)
                {
                    if (recipes[i].Ingredients.Length != ingredientsLen)
                    {
                        throw new Exception("Barrel recipe with same name but different ingredient count! Sorry, this is not supported right now. Please make sure you choose different barrel recipe names if you have different ingredient counts.");
                    }



                    for (int j = 0; j < ingredientsLen; j++)
                    {
                        if (i == 0)
                        {
                            ingstacks[j] = new ItemStack[recipes.Count];
                        }

                        ingstacks[j][i] = recipes[i].Ingredients[j].ResolvedItemstack;
                    }
                }

                for (int i = 0; i < ingredientsLen; i++)
                {
                    if (i > 0)
                    {
                        RichTextComponent cmp = new RichTextComponent(capi, "+", CairoFont.WhiteMediumText());
                        cmp.VerticalAlign = EnumVerticalAlign.Middle;
                        barrelRecipestext.Add(cmp);
                    }

                    SlideshowItemstackTextComponent scmp = new SlideshowItemstackTextComponent(capi, ingstacks[i], 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                    scmp.ShowStackSize = true;
                    barrelRecipestext.Add(scmp);
                }


                barrelRecipestext.Add(new ClearFloatTextComponent(capi, 10));
            }





            string customCreatedBy = stack.Collectible.Attributes?["handbook"]?["createdBy"]?.AsString(null);

            if (grecipes.Count > 0 || smithable || knappable || clayformable || customCreatedBy != null || bakables.Count > 0 || barrelRecipestext.Count > 0 || grindables.Count > 0 || curables.Count > 0 || ripenables.Count > 0 || crushables.Count > 0)
            {
                components.Add(new ClearFloatTextComponent(capi, 10));
                components.Add(new RichTextComponent(capi, Lang.Get("Created by") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));


                if (smithable)
                {
                    components.Add(new RichTextComponent(capi, "• ", CairoFont.WhiteSmallText()));
                    components.Add(new LinkTextComponent(capi, Lang.Get("Smithing") + "\n", CairoFont.WhiteSmallText(), (cs) => { openDetailPageFor("craftinginfo-smithing"); }));
                }
                if (knappable)
                {
                    components.Add(new RichTextComponent(capi, "• ", CairoFont.WhiteSmallText()));
                    components.Add(new LinkTextComponent(capi, Lang.Get("Knapping") + "\n", CairoFont.WhiteSmallText(), (cs) => { openDetailPageFor("craftinginfo-knapping"); }));
                }
                if (clayformable)
                {
                    components.Add(new RichTextComponent(capi, "• ", CairoFont.WhiteSmallText()));
                    components.Add(new LinkTextComponent(capi, Lang.Get("Clay forming") + "\n", CairoFont.WhiteSmallText(), (cs) => { openDetailPageFor("craftinginfo-clayforming"); }));
                }
                if (customCreatedBy != null)
                {
                    components.Add(new RichTextComponent(capi, "• ", CairoFont.WhiteSmallText()));
                    components.AddRange(VtmlUtil.Richtextify(capi, Lang.Get(customCreatedBy) + "\n", CairoFont.WhiteSmallText()));
                }

                if (grindables.Count > 0)
                {
                    components.Add(new RichTextComponent(capi, "• " + Lang.Get("Grinding") + "\n", CairoFont.WhiteSmallText()));

                    while (grindables.Count > 0)
                    {
                        ItemStack dstack = grindables[0];
                        grindables.RemoveAt(0);
                        if (dstack == null)
                            continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, grindables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                }

                if (crushables.Count > 0)
                {
                    components.Add(new RichTextComponent(capi, "• " + Lang.Get("Crushing") + "\n", CairoFont.WhiteSmallText()));

                    while (crushables.Count > 0)
                    {
                        ItemStack dstack = crushables[0];
                        crushables.RemoveAt(0);
                        if (dstack == null)
                            continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, crushables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                }


                if (curables.Count > 0)
                {
                    components.Add(new RichTextComponent(capi, "• " + Lang.Get("Curing") + "\n", CairoFont.WhiteSmallText()));

                    while (curables.Count > 0)
                    {
                        ItemStack dstack = curables[0];
                        curables.RemoveAt(0);
                        if (dstack == null)
                            continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, curables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        components.Add(comp);
                    }
                }



                if (ripenables.Count > 0)
                {
                    components.Add(new RichTextComponent(capi, "• " + Lang.Get("Ripening") + "\n", CairoFont.WhiteSmallText()));

                    while (ripenables.Count > 0)
                    {
                        ItemStack dstack = ripenables[0];
                        ripenables.RemoveAt(0);
                        if (dstack == null)
                            continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, ripenables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }


                if (bakables.Count > 0)
                {
                    components.Add(new RichTextComponent(capi, "• " + Lang.Get("Cooking/Smelting/Baking") + "\n", CairoFont.WhiteSmallText()));

                    while (bakables.Count > 0)
                    {
                        ItemStack dstack = bakables[0];
                        bakables.RemoveAt(0);
                        if (dstack == null)
                            continue;

                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, bakables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                        components.Add(comp);
                    }

                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }


                if (grecipes.Count > 0)
                {

                    // COMMENT THIS LINE OUT ONLY if (knappable) - whats this for? o.O 
                    components.Add(new RichTextComponent(capi, "• " + Lang.Get("Crafting") + "\n", CairoFont.WhiteSmallText()));

                    components.Add(new SlideshowGridRecipeTextComponent(capi, grecipes.ToArray(), 40, EnumFloat.None, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)), allStacks));
                }

                if (barrelRecipestext.Count > 0)
                {
                    components.Add(new RichTextComponent(capi, "• " + Lang.Get("In Barrel") + "\n", CairoFont.WhiteSmallText()));
                    components.AddRange(barrelRecipestext);
                }
            }

            JsonObject obj = stack.Collectible.Attributes?["handbook"]?["extraSections"];
            if (obj != null && obj.Exists)
            {
                ExtraSection[] sections = obj?.AsObject<ExtraSection[]>();
                for (int i = 0; i < sections.Length; i++)
                {
                    components.Add(new ClearFloatTextComponent(capi, 10));
                    components.Add(new RichTextComponent(capi, Lang.Get(sections[i].Title) + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));

                    components.AddRange(VtmlUtil.Richtextify(capi, Lang.Get(sections[i].Text) + "\n", CairoFont.WhiteSmallText()));
                }
            }

            string type = stack.Class == EnumItemClass.Block ? "block" : "item";
            string code = Code.ToShortString();
            string langExtraSectionTitle = Lang.GetMatchingIfExists(Code.Domain + ":" + type + "-handbooktitle-" + code);
            string langExtraSectionText = Lang.GetMatchingIfExists(Code.Domain + ":" + type + "-handbooktext-" + code);

            if (langExtraSectionTitle != null || langExtraSectionText != null)
            {
                components.Add(new ClearFloatTextComponent(capi, 10));
                if (langExtraSectionTitle != null)
                {
                    components.Add(new RichTextComponent(capi, langExtraSectionTitle + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                }
                if (langExtraSectionText != null)
                {
                    components.AddRange(VtmlUtil.Richtextify(capi, langExtraSectionText + "\n", CairoFont.WhiteSmallText()));
                }
            }

            return components.ToArray();
        }

        // Get Nutrition Properties for a SINGLE STACK
        // SPANG - March 13, 2022
        public static FoodNutritionProperties[] GetExpandedContentNutritionProperties(IWorldAccessor world, ItemSlot inSlot, ItemStack contentStack, EntityAgent forEntity, bool mulWithStacksize = false, float nutritionMul = 1f, float healthMul = 1f)
        {
            List<FoodNutritionProperties> foodProps = new List<FoodNutritionProperties>();

            CollectibleObject obj = contentStack.Collectible;
            FoodNutritionProperties stackProps;

            if (obj.CombustibleProps != null && obj.CombustibleProps.SmeltedStack != null)
            {
                stackProps = obj.CombustibleProps.SmeltedStack.ResolvedItemstack.Collectible.GetNutritionProperties(world, obj.CombustibleProps.SmeltedStack.ResolvedItemstack, forEntity);
            }
            else
            {
                stackProps = obj.GetNutritionProperties(world, contentStack, forEntity);
            }

            if (obj.Attributes?["nutritionPropsWhenInMeal"].Exists == true)
            {
                stackProps = obj.Attributes?["nutritionPropsWhenInMeal"].AsObject<FoodNutritionProperties>();
            }
            if (obj.Attributes?["nutritionPropsWhenInPie"].Exists == true && mulWithStacksize)
            {
                stackProps = obj.Attributes?["nutritionPropsWhenInPie"].AsObject<FoodNutritionProperties>();
            }
            float satLossMul = 1.0f;
            float healthLoss = 1.0f;
            float mul = mulWithStacksize ? contentStack.StackSize : 1;
            if (BlockLiquidContainerBase.GetContainableProps(contentStack) != null && mulWithStacksize)
            {
                mul /= 10;
            }
            if (obj is ItemExpandedRawFood && (contentStack.Attributes["expandedSats"] as FloatArrayAttribute)?.value?.Length == 6)
            {
                FoodNutritionProperties[] exProps = (obj as ItemExpandedRawFood).GetPropsFromArray((contentStack.Attributes["expandedSats"] as FloatArrayAttribute).value);

                if (exProps != null || exProps.Length > 0)
                {
                    foreach (FoodNutritionProperties exProp in exProps)
                    {
                        exProp.Satiety *= satLossMul * nutritionMul * (obj is ItemExpandedLiquid ? contentStack.StackSize / 10 : 1 * mul);
                        exProp.Health *= healthLoss * healthMul * (obj is ItemExpandedLiquid ? contentStack.StackSize / 10 : 1 * mul);

                        foodProps.Add(exProp);
                    }
                }
                if (stackProps != null)
                {
                    FoodNutritionProperties props = stackProps.Clone();
                    props.Satiety *= satLossMul * nutritionMul * mul;
                    props.Health *= healthLoss * healthMul * mul;
                    foodProps.Add(props);
                }
            }
            else if (stackProps != null)
            {
                FoodNutritionProperties props = stackProps.Clone();

                DummySlot slot = new DummySlot(contentStack, inSlot.Inventory);
                TransitionState state = contentStack.Collectible.UpdateAndGetTransitionState(world, slot, EnumTransitionType.Perish);
                float spoilState = state != null ? state.TransitionLevel : 0;

                satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, forEntity);
                healthLoss = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, forEntity);
                props.Satiety *= satLossMul * nutritionMul * mul;
                props.Health *= healthLoss * healthMul * mul;
                foodProps.Add(props);
            }
            return foodProps.ToArray();
        }

        class ExtraSection { public string Title = null; public string Text = null; }

        public override TransitionState[] UpdateAndGetTransitionStates(IWorldAccessor world, ItemSlot inslot)
        {
            ItemStack itemstack = inslot.Itemstack;

            TransitionableProperties[] propsm = GetTransitionableProperties(world, inslot.Itemstack, null);

            if (itemstack == null || propsm == null || propsm.Length == 0)
            {
                return null;
            }

            if (itemstack.Attributes == null)
            {
                itemstack.Attributes = new TreeAttribute();
            }

            if (!(itemstack.Attributes["transitionstate"] is ITreeAttribute))
            {
                itemstack.Attributes["transitionstate"] = new TreeAttribute();
            }

            ITreeAttribute attr = (ITreeAttribute)itemstack.Attributes["transitionstate"];

            //TransitionableProperties[] props = itemstack.Collectible.TransitionableProps; - WTF is this here for? we already have props

            float[] transitionedHours;
            float[] freshHours;
            float[] transitionHours;
            TransitionState[] states = new TransitionState[propsm.Length];

            if (!attr.HasAttribute("createdTotalHours"))
            {
                attr.SetDouble("createdTotalHours", world.Calendar.TotalHours);
                attr.SetDouble("lastUpdatedTotalHours", world.Calendar.TotalHours);

                freshHours = new float[propsm.Length];
                transitionHours = new float[propsm.Length];
                transitionedHours = new float[propsm.Length];

                for (int i = 0; i < propsm.Length; i++)
                {
                    transitionedHours[i] = 0;
                    if (propsm[i] != null)
                    {
                        freshHours[i] = propsm[i].FreshHours.nextFloat(1, world.Rand);
                        transitionHours[i] = propsm[i].TransitionHours.nextFloat(1, world.Rand);
                    }
                    else
                    {
                        freshHours[i] = 0;
                        transitionHours[i] = 0;
                    }
                }

                attr["freshHours"] = new FloatArrayAttribute(freshHours);
                attr["transitionHours"] = new FloatArrayAttribute(transitionHours);
                attr["transitionedHours"] = new FloatArrayAttribute(transitionedHours);
            }
            else
            {
                freshHours = (attr["freshHours"] as FloatArrayAttribute).value;
                transitionHours = (attr["transitionHours"] as FloatArrayAttribute).value;
                transitionedHours = (attr["transitionedHours"] as FloatArrayAttribute).value;
            }

            double lastUpdatedTotalHours = attr.GetDouble("lastUpdatedTotalHours");
            double nowTotalHours = world.Calendar.TotalHours;


            bool nowSpoiling = false;

            float hoursPassed = (float)(nowTotalHours - lastUpdatedTotalHours);

            for (int i = 0; i < propsm.Length; i++)
            {
                TransitionableProperties prop = propsm[i];
                if (prop == null || i >= freshHours.Length)
                    continue;

                float transitionRateMul = GetTransitionRateMul(world, inslot, prop.Type);

                if (hoursPassed > 0.05f) // Maybe prevents us from running into accumulating rounding errors?
                {
                    float hoursPassedAdjusted = hoursPassed * transitionRateMul;
                    transitionedHours[i] += hoursPassedAdjusted;

                    /*if (api.World.Side == EnumAppSide.Server && inslot.Inventory.ClassName == "chest")
                    {
                        Console.WriteLine(hoursPassed + " hours passed. " + inslot.Itemstack.Collectible.Code + " spoil by " + transitionRateMul + "x. Is inside " + inslot.Inventory.ClassName + " {0}/{1}", transitionedHours[i], freshHours[i]);
                    }*/
                }

                float freshHoursLeft = Math.Max(0, freshHours[i] - transitionedHours[i]);
                float transitionLevel = Math.Max(0, transitionedHours[i] - freshHours[i]) / transitionHours[i];

                // Don't continue transitioning spoiled foods
                if (transitionLevel > 0)
                {
                    if (prop.Type == EnumTransitionType.Perish)
                    {
                        nowSpoiling = true;
                    }
                    else
                    {
                        if (nowSpoiling)
                            continue;
                    }
                }

                if (transitionLevel >= 1 && world.Side == EnumAppSide.Server)
                {
                    ItemStack newstack = OnTransitionNow(inslot, itemstack.Collectible.TransitionableProps[i]);

                    if (newstack.StackSize <= 0)
                    {
                        inslot.Itemstack = null;
                    }
                    else
                    {
                        itemstack.SetFrom(newstack);
                    }

                    inslot.MarkDirty();

                    // Only do one transformation, then do the next one next update
                    // This does fully not respect time-fast-forward, so that should be fixed some day
                    break;
                }

                states[i] = new TransitionState()
                {
                    FreshHoursLeft = freshHoursLeft,
                    TransitionLevel = Math.Min(1, transitionLevel),
                    TransitionedHours = transitionedHours[i],
                    TransitionHours = transitionHours[i],
                    FreshHours = freshHours[i],
                    Props = prop
                };

                //if (transitionRateMul > 0) break; // Only do one transformation at the time (i.e. food can not cure and perish at the same time) - Tyron 9/oct 2020, but why not at the same time? We need it for cheese ripening
            }

            if (hoursPassed > 0.05f)
            {
                attr.SetDouble("lastUpdatedTotalHours", nowTotalHours);
            }

            return states.Where(s => s != null).OrderBy(s => (int)s.Props.Type).ToArray();
        }

        public override TransitionState UpdateAndGetTransitionState(IWorldAccessor world, ItemSlot inslot, EnumTransitionType type)
        {
            TransitionState[] states = UpdateAndGetTransitionStates(world, inslot);
            TransitionableProperties[] propsm = GetTransitionableProperties(world, inslot?.Itemstack, null);
            if (propsm == null)
                return null;

            for (int i = 0; i < propsm.Length; i++)
            {
                if (i >= states.Length)
                    break;
                if (propsm[i]?.Type == type)
                    return states[i];
            }

            return null;
        }

        public void OnBaked(ItemStack oldStack, ItemStack newStack)
        {
            string[] ings = (oldStack?.Attributes["madeWith"] as StringArrayAttribute)?.value;
            float[] sats = (oldStack?.Attributes["expandedSats"] as FloatArrayAttribute)?.value;
            if (ings != null) newStack.Attributes["madeWith"] = new StringArrayAttribute(ings);
            if (sats != null) newStack.Attributes["expandedSats"] = new FloatArrayAttribute(sats);
        }
        public void OnCreatedByGrinding(ItemStack input, ItemStack output)
        {
            string[] ings = (input?.Attributes["madeWith"] as StringArrayAttribute)?.value;
            float[] sats = (input?.Attributes["expandedSats"] as FloatArrayAttribute)?.value;


            //dividedSats.Foreach(sat => { sat /= output.StackSize;});
            if (ings != null) output.Attributes["madeWith"] = new StringArrayAttribute(ings);
            if (sats != null)
            {
                float[] dividedSats = Array.ConvertAll(sats, i => i / output.StackSize);
                output.Attributes["expandedSats"] = new FloatArrayAttribute(dividedSats);
            }

        }
        public void OnHandbookPageComposed(List<RichTextComponentBase> components, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
        }
    }

    public interface IExpandedFood
    {
        void OnCreatedByKneading(List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> input, ItemStack output);
        void OnCreatedByGrinding(ItemStack input, ItemStack output);
    }

    public enum EnumNutritionMatch
    {
        Hp,
        Fruit,
        Grain,
        Vegetable,
        Protein,
        Dairy
    }



    /*************************************************************************************************************/
    public class EFTextureSource : ITexPositionSource
    {
        public ItemStack forContents;
        private readonly ICoreClientAPI capi;
        private TextureAtlasPosition contentTextPos;
        private readonly TextureAtlasPosition blockTextPos;
        private readonly TextureAtlasPosition corkTextPos;
        private readonly CompositeTexture contentTexture;

        public EFTextureSource(ICoreClientAPI capi, ItemStack forContents, CompositeTexture contentTexture)
        {
            this.capi = capi;
            this.forContents = forContents;
            this.contentTexture = contentTexture;
        }


        ITextureAtlasAPI curAtlas;
        Shape nowTesselatingShape;

        public Size2i AtlasSize => this.capi.BlockTextureAtlas.Size;
        public virtual TextureAtlasPosition this[string textureCode]
        {
            get
            {
                AssetLocation texturePath = null;
                CompositeTexture tex;

                if (this.contentTextPos == null)
                {
                    int textureSubId;
                    textureSubId = ObjectCacheUtil.GetOrCreate<int>(this.capi, "efcontenttexture-" + this.contentTexture.ToString(), () =>
                    {
                        var id = 0;
                        var bmp = this.capi.Assets.TryGet(this.contentTexture.Base.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"))?.ToBitmap(this.capi);

                        if (bmp != null)
                        {
                            //if (contentTexture.Alpha != 255)
                            //{ bmp.MulAlpha(contentTexture.Alpha); }

                            // for now, a try catch will have to suffice - barf
                            try
                            {
                                this.capi.BlockTextureAtlas.InsertTexture(bmp, out id, out var texPos);
                            }
                            catch
                            { }
                            bmp.Dispose();
                        }
                        return id;
                    });
                    this.contentTextPos = this.capi.BlockTextureAtlas.Positions[textureSubId];
                }
                return this.contentTextPos;
            }
        }
    }
}
