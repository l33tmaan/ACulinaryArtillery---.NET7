using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using Microsoft.Win32;

namespace ACulinaryArtillery
{
    public class acaRecipeNames : ICookingRecipeNamingHelper
    {
        public string GetNameForIngredients(IWorldAccessor worldForResolve, string recipeCode, ItemStack[] stacks)
        {
            string mealName = Lang.Get("aculinaryartillery:meal-normal-" + recipeCode);
            string full = " ";

            List<string> ings = new List<string>();

            if (stacks == null || stacks.Length <= 0) return mealName;
            foreach (ItemStack stack in stacks)
            {
                if (!ings.Contains(Lang.Get("recipeingredient-" + (stack.Block != null ? "block-" : "item-") + stack.Collectible.Code.Path)))
                    ings.Add(Lang.Get("recipeingredient-" + (stack.Block != null ? "block-" : "item-") + stack.Collectible.Code.Path));
            }

            if (ings.Count == 1) return mealName + full + Lang.Get("aculinaryartillery:made with ") + ings[0];

            full = mealName + full + Lang.Get("aculinaryartillery:made with ");

            for (int i = 0; i < ings.Count; i++)
            {
                if (i + 1 == ings.Count)
                {
                    full += Lang.Get("aculinaryartillery:and ") + ings[i];
                }
                else
                {
                    full += ings[i] + ", ";
                }
            }


            return full;
        }
    }
    /*
    public sealed class MixingRecipeRegistry
    {
        private MixingRecipeRegistry()
        {
            //do our intialisation stuff, only once!
        }

        private static readonly MixingRecipeRegistry registry = new MixingRecipeRegistry();
        public static MixingRecipeRegistry Registry
        {
            get
            {
                return registry;
            }
        }

        private List<CookingRecipe> mixingRecipes = new List<CookingRecipe>();
        private List<DoughRecipe> kneadingRecipes = new List<DoughRecipe>();
        private List<SimmerRecipe> simmerRecipes = new List<SimmerRecipe>();

        public List<CookingRecipe> MixingRecipes
        {
            get
            {
                return mixingRecipes;
            }
            set
            {
                mixingRecipes = value;
            }
        }
        public List<DoughRecipe> KneadingRecipes
        {
            get
            {
                return kneadingRecipes;
            }
            set
            {
                kneadingRecipes = value;
            }
        }
        public List<SimmerRecipe> SimmerRecipes
        {
            get
            {
                return simmerRecipes;
            }
            set
            {
                simmerRecipes = value;
            }
        }
    }
    */
    public class DoughRecipe : IByteSerializable
    {
        public string Code = "something";
        public AssetLocation Name { get; set; }
        public bool Enabled { get; set; } = true;


        public DoughIngredient[] Ingredients;

        public JsonItemStack Output;

        public ItemStack TryCraftNow(ICoreAPI api, ItemSlot[] inputslots)
        {
            var matched = pairInput(inputslots);
            
            ItemStack mixedStack = Output.ResolvedItemstack.Clone();
            mixedStack.StackSize = getOutputSize(matched);

            if (mixedStack.StackSize <= 0) return null;

            /*
            TransitionableProperties[] props = mixedStack.Collectible.GetTransitionableProperties(api.World, mixedStack, null);
            TransitionableProperties perishProps = props != null && props.Length > 0 ? props[0] : null;

            if (perishProps != null)
            {
                CollectibleObject.CarryOverFreshness(api, inputslots, new ItemStack[] { mixedStack }, perishProps);
            }*/

            IExpandedFood food;
            if ((food = mixedStack.Collectible as IExpandedFood) != null) food.OnCreatedByKneading(matched, mixedStack);

            foreach (var val in matched)
            {
                val.Key.TakeOut(val.Value.Quantity * (mixedStack.StackSize / Output.StackSize));
                val.Key.MarkDirty();
            }
            
            return mixedStack;
        }

        public bool Matches(IWorldAccessor worldForResolve, ItemSlot[] inputSlots)
        {
            int outputStackSize = 0;

            List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = pairInput(inputSlots);
            if (matched == null) return false;

            outputStackSize = getOutputSize(matched);

            return outputStackSize >= 0;
        }

        List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> pairInput(ItemSlot[] inputStacks)
        {
            List<int> alreadyFound = new List<int>();

            Queue<ItemSlot> inputSlotsList = new Queue<ItemSlot>();
            foreach (var val in inputStacks) if (!val.Empty) inputSlotsList.Enqueue(val);

            if (inputSlotsList.Count != Ingredients.Length) return null;

            List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = new List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>>();

            while (inputSlotsList.Count > 0)
            {
                ItemSlot inputSlot = inputSlotsList.Dequeue();
                bool found = false;

                for (int i = 0; i < Ingredients.Length; i++)
                {
                    CraftingRecipeIngredient ingred = Ingredients[i].GetMatch(inputSlot.Itemstack);

                    if (ingred != null && !alreadyFound.Contains(i))
                    {
                        matched.Add(new KeyValuePair<ItemSlot, CraftingRecipeIngredient>(inputSlot, ingred));
                        alreadyFound.Add(i);
                        found = true;
                        break;
                    }
                }

                if (!found) return null;
            }

            // We're missing ingredients
            if (matched.Count != Ingredients.Length)
            {
                return null;
            }

            return matched;
        }


        int getOutputSize(List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched)
        {
            int outQuantityMul = -1;

            foreach (var val in matched)
            {
                ItemSlot inputSlot = val.Key;
                CraftingRecipeIngredient ingred = val.Value;
                int posChange = inputSlot.StackSize / ingred.Quantity;

                if (posChange < outQuantityMul || outQuantityMul == -1) outQuantityMul = posChange;
            }

            if (outQuantityMul == -1)
            {
                return -1;
            }


            foreach (var val in matched)
            {
                ItemSlot inputSlot = val.Key;
                CraftingRecipeIngredient ingred = val.Value;


                // Must have same or more than the total crafted amount
                if (inputSlot.StackSize < ingred.Quantity * outQuantityMul) return -1;

            }

            outQuantityMul = 1;
            return Output.StackSize * outQuantityMul;
        }

        public string GetOutputName()
        {
            return Lang.Get("aculinaryartillery:Will make {0}", Output.ResolvedItemstack.GetName());
        }

        public bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
        {
            bool ok = true;

            for (int i = 0; i < Ingredients.Length; i++)
            {
                ok &= Ingredients[i].Resolve(world, sourceForErrorLogging);
            }

            ok &= Output.Resolve(world, sourceForErrorLogging);


            return ok;
        }

        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(Code);
            writer.Write(Ingredients.Length);
            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i].ToBytes(writer);
            }

            Output.ToBytes(writer);
        }

        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            Code = reader.ReadString();
            Ingredients = new DoughIngredient[reader.ReadInt32()];

            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i] = new DoughIngredient();
                Ingredients[i].FromBytes(reader, resolver);
                Ingredients[i].Resolve(resolver, "Dough Recipe (FromBytes)");
            }

            Output = new JsonItemStack();
            Output.FromBytes(reader, resolver.ClassRegistry);
            Output.Resolve(resolver, "Dough Recipe (FromBytes)");
        }

        public DoughRecipe Clone()
        {
            DoughIngredient[] ingredients = new DoughIngredient[Ingredients.Length];
            for (int i = 0; i < Ingredients.Length; i++)
            {
                ingredients[i] = Ingredients[i].Clone();
            }

            return new DoughRecipe()
            {
                Output = Output.Clone(),
                Code = Code,
                Enabled = Enabled,
                Name = Name,
                Ingredients = ingredients
            };
        }

        public Dictionary<string, string[]> GetNameToCodeMapping(IWorldAccessor world)
        {
            Dictionary<string, string[]> mappings = new Dictionary<string, string[]>();

            if (Ingredients == null || Ingredients.Length == 0) return mappings;

            foreach (var ingreds in Ingredients)
            {
                if (ingreds.Inputs.Length <= 0) continue;
                CraftingRecipeIngredient ingred = ingreds.Inputs[0];
                if (ingred == null || !ingred.Code.Path.Contains("*") || ingred.Name == null) continue;

                int wildcardStartLen = ingred.Code.Path.IndexOf("*");
                int wildcardEndLen = ingred.Code.Path.Length - wildcardStartLen - 1;

                List<string> codes = new List<string>();

                if (ingred.Type == EnumItemClass.Block)
                {
                    for (int i = 0; i < world.Blocks.Count; i++)
                    {
                        if (world.Blocks[i].Code == null || world.Blocks[i].IsMissing) continue;

                        if (WildcardUtil.Match(ingred.Code, world.Blocks[i].Code))
                        {
                            string code = world.Blocks[i].Code.Path.Substring(wildcardStartLen);
                            string codepart = code.Substring(0, code.Length - wildcardEndLen);
                            if (ingred.AllowedVariants != null && !ingred.AllowedVariants.Contains(codepart)) continue;

                            codes.Add(codepart);

                        }
                    }
                }
                else
                {
                    for (int i = 0; i < world.Items.Count; i++)
                    {
                        if (world.Items[i].Code == null || world.Items[i].IsMissing) continue;

                        if (WildcardUtil.Match(ingred.Code, world.Items[i].Code))
                        {
                            string code = world.Items[i].Code.Path.Substring(wildcardStartLen);
                            string codepart = code.Substring(0, code.Length - wildcardEndLen);
                            if (ingred.AllowedVariants != null && !ingred.AllowedVariants.Contains(codepart)) continue;

                            codes.Add(codepart);
                        }
                    }
                }

                mappings[ingred.Name] = codes.ToArray();
            }

            return mappings;
        }
    }
    /*
    public class acaRecipeLoader : RecipeLoader
    {
        public ICoreServerAPI api;

        public override double ExecuteOrder()
        {
            return 100;
        }

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;  //we only load recipes on the server
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            MixingRecipeRegistry.Registry.MixingRecipes.Clear();
            MixingRecipeRegistry.Registry.KneadingRecipes.Clear();
            MixingRecipeRegistry.Registry.SimmerRecipes.Clear();
            if (!(api is ICoreServerAPI sapi)) return;
            LoadFoodRecipes();
        }

        public void LoadFoodRecipes()
        {
            //LoadMixingRecipes();
            //LoadKneadingRecipes();
            //LoadSimmeringRecipes();
        }
        public override void AssetsLoaded(ICoreAPI api)
        {
            //override to prevent double loading
            if (!(api is ICoreServerAPI sapi)) return;
            this.api = sapi;
        }

        public void LoadMixingRecipes()
        {
            Dictionary<AssetLocation, JToken> files = api.Assets.GetMany<JToken>(api.Server.Logger, "recipes/mixing");
            int recipeQuantity = 0;

            foreach (var val in files)
            {
                String recCode = null;
                if (val.Value is JObject)
                {
                    CookingRecipe rec = val.Value.ToObject<CookingRecipe>();
                    if (!rec.Enabled) continue;

                    rec.Resolve(api.World, "mixing recipe " + val.Key);
                    MixingRecipeRegistry.Registry.MixingRecipes.Add(rec);
                    recCode = rec.Code;
                    recipeQuantity++;
                }
                if (val.Value is JArray)
                {
                    foreach (var token in (val.Value as JArray))
                    {
                        CookingRecipe rec = token.ToObject<CookingRecipe>();
                        if (!rec.Enabled) continue;

                        rec.Resolve(api.World, "mixing recipe " + val.Key);
                        MixingRecipeRegistry.Registry.MixingRecipes.Add(rec);
                        recCode = rec.Code;
                        recipeQuantity++;
                    }
                }
                if (recCode is not null && !CookingRecipe.NamingRegistry.ContainsKey(recCode))
                {
                    //CookingRecipe.NamingRegistry[recCode] = new acaRecipeNames();
                }
            }
            //ICookingRecipeNamingHelper value;
            
            //CookingRecipe.NamingRegistry.TryGetValue("meatysalad", out value);
           
            //ACulinaryArtillery.logger.Debug("It worked?: " + (value is not null));
            api.World.Logger.Event("{0} mixing recipes loaded", recipeQuantity);
            api.World.Logger.StoryEvent(Lang.Get("aculinaryartillery:The chef and the apprentice..."));
        }

        public void LoadKneadingRecipes()
        {
            Dictionary<AssetLocation, JToken> files = api.Assets.GetMany<JToken>(api.Server.Logger, "recipes/kneading");
            int recipeQuantity = 0;
            int ignored = 0;

            foreach (var val in files)
            {
                if (val.Value is JObject)
                {
                    DoughRecipe rec = val.Value.ToObject<DoughRecipe>();
                    if (!rec.Enabled) continue;

                    LoadKneadingRecipe(val.Key, rec, ref recipeQuantity, ref ignored);
                }
                if (val.Value is JArray)
                {
                    foreach (var token in (val.Value as JArray))
                    {
                        DoughRecipe rec = token.ToObject<DoughRecipe>();
                        if (!rec.Enabled) continue;

                        LoadKneadingRecipe(val.Key, rec, ref recipeQuantity, ref ignored);
                    }
                }
            }

            api.World.Logger.Event("{0} kneading recipes loaded", recipeQuantity);
            api.World.Logger.StoryEvent(Lang.Get("aculinaryartillery:The butter and the bread..."));
        }

        public void LoadSimmeringRecipes()
        {
            Dictionary<AssetLocation, JToken> files = api.Assets.GetMany<JToken>(api.Server.Logger, "recipes/simmering");
            int recipeQuantity = 0;
            int ignored = 0;

            foreach (var val in files)
            {
                if (val.Value is JObject)
                {
                    SimmerRecipe rec = val.Value.ToObject<SimmerRecipe>();
                    if (!rec.Enabled) continue;

                    LoadSimmeringRecipe(val.Key, rec, ref recipeQuantity, ref ignored);
                }
                if (val.Value is JArray)
                {
                    foreach (var token in (val.Value as JArray))
                    {
                        SimmerRecipe rec = token.ToObject<SimmerRecipe>();
                        if (!rec.Enabled) continue;

                        LoadSimmeringRecipe(val.Key, rec, ref recipeQuantity, ref ignored);
                    }
                }
            }

            api.World.Logger.Event("{0} simmering recipes loaded", recipeQuantity);
            api.World.Logger.StoryEvent(Lang.Get("aculinaryartillery:The syrup and lard..."));
        }


        void LoadSimmeringRecipe(AssetLocation path, SimmerRecipe recipe, ref int quantityRegistered, ref int quantityIgnored)
        {
            if (!recipe.Enabled) return;
            if (recipe.Name == null) recipe.Name = path;
            string className = "simmer recipe";

            Dictionary<string, string[]> nameToCodeMapping = recipe.GetNameToCodeMapping(api.World);

            if (nameToCodeMapping.Count > 0)
            {
                List<SimmerRecipe> subRecipes = new List<SimmerRecipe>();

                int qCombs = 0;
                bool first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    if (first) qCombs = val2.Value.Length;
                    else qCombs *= val2.Value.Length;
                    first = false;
                }

                first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    string variantCode = val2.Key;
                    string[] variants = val2.Value;

                    for (int i = 0; i < qCombs; i++)
                    {
                        SimmerRecipe rec;

                        if (first) subRecipes.Add(rec = recipe.Clone());
                        else rec = subRecipes[i];

                        if (rec.Ingredients != null)
                        {
                            foreach (var ingreds in rec.Ingredients)
                            {
                                if (rec.Ingredients.Length <= 0) continue;
                                CraftingRecipeIngredient ingred = ingreds;

                                if (ingred.Name == variantCode)
                                {
                                    ingred.Code = ingred.Code.CopyWithPath(ingred.Code.Path.Replace("*", variants[i % variants.Length]));
                                }
                            }
                        }

                        rec.Simmering.SmeltedStack.FillPlaceHolder(val2.Key, variants[i % variants.Length]);
                    }

                    first = false;
                }

                if (subRecipes.Count == 0)
                {
                    api.World.Logger.Warning("{1} file {0} make uses of wildcards, but no blocks or item matching those wildcards were found.", path, className);
                }

                foreach (SimmerRecipe subRecipe in subRecipes)
                {
                    if (!subRecipe.Resolve(api.World, className + " " + path))
                    {
                        quantityIgnored++;
                        continue;
                    }
                    MixingRecipeRegistry.Registry.SimmerRecipes.Add(subRecipe);
                    quantityRegistered++;
                }

            }
            else
            {
                if (!recipe.Resolve(api.World, className + " " + path))
                {
                    quantityIgnored++;
                    return;
                }

                MixingRecipeRegistry.Registry.SimmerRecipes.Add(recipe);
                quantityRegistered++;
            }
        }


        void LoadKneadingRecipe(AssetLocation path, DoughRecipe recipe, ref int quantityRegistered, ref int quantityIgnored)
        {
            if (!recipe.Enabled) return;
            if (recipe.Name == null) recipe.Name = path;
            string className = "kneading recipe";

            Dictionary<string, string[]> nameToCodeMapping = recipe.GetNameToCodeMapping(api.World);

            if (nameToCodeMapping.Count > 0)
            {
                List<DoughRecipe> subRecipes = new List<DoughRecipe>();

                int qCombs = 0;
                bool first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    if (first) qCombs = val2.Value.Length;
                    else qCombs *= val2.Value.Length;
                    first = false;
                }

                first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    string variantCode = val2.Key;
                    string[] variants = val2.Value;

                    for (int i = 0; i < qCombs; i++)
                    {
                        DoughRecipe rec;

                        if (first) subRecipes.Add(rec = recipe.Clone());
                        else rec = subRecipes[i];

                        if (rec.Ingredients != null)
                        {
                            foreach (var ingreds in rec.Ingredients)
                            {
                                if (ingreds.Inputs.Length <= 0) continue;
                                CraftingRecipeIngredient ingred = ingreds.Inputs[0];

                                if (ingred.Name == variantCode)
                                {
                                    ingred.Code = ingred.Code.CopyWithPath(ingred.Code.Path.Replace("*", variants[i % variants.Length]));
                                }
                            }
                        }

                        rec.Output.FillPlaceHolder(val2.Key, variants[i % variants.Length]);
                    }

                    first = false;
                }

                if (subRecipes.Count == 0)
                {
                    api.World.Logger.Warning("{1} file {0} make uses of wildcards, but no blocks or item matching those wildcards were found.", path, className);
                }

                foreach (DoughRecipe subRecipe in subRecipes)
                {
                    if (!subRecipe.Resolve(api.World, className + " " + path))
                    {
                        quantityIgnored++;
                        continue;
                    }
                    MixingRecipeRegistry.Registry.KneadingRecipes.Add(subRecipe);
                    quantityRegistered++;
                }

            }
            else
            {
                if (!recipe.Resolve(api.World, className + " " + path))
                {
                    quantityIgnored++;
                    return;
                }

                MixingRecipeRegistry.Registry.KneadingRecipes.Add(recipe);
                quantityRegistered++;
            }
        }


    }
    */
    public class SimmerRecipe : IByteSerializable
    {
        public string Code = "something";
        public AssetLocation Name { get; set; }
        public bool Enabled { get; set; } = true;


        public CraftingRecipeIngredient[] Ingredients;

        public CombustibleProperties Simmering;

        //public JsonItemStack Output;

        public ItemStack TryCraftNow(ICoreAPI api, ItemSlot[] inputslots)
        {

            var matched = pairInput(inputslots);

            ItemStack mixedStack = Simmering.SmeltedStack.ResolvedItemstack.Clone();
            mixedStack.StackSize = getOutputSize(matched);

            if (mixedStack.StackSize <= 0) return null;

            /*
            TransitionableProperties[] props = mixedStack.Collectible.GetTransitionableProperties(api.World, mixedStack, null);
            TransitionableProperties perishProps = props != null && props.Length > 0 ? props[0] : null;

            if (perishProps != null)
            {
                CollectibleObject.CarryOverFreshness(api, inputslots, new ItemStack[] { mixedStack }, perishProps);
            }*/

            foreach (var val in matched)
            {

                val.Key.TakeOut(val.Value.Quantity * (mixedStack.StackSize / Simmering.SmeltedStack.StackSize));
                val.Key.MarkDirty();
            }

            return mixedStack;
        }

        public bool Matches(IWorldAccessor worldForResolve, ItemSlot[] inputSlots)
        {
            int outputStackSize = 0;

            List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = pairInput(inputSlots);
            if (matched == null) return false;

            outputStackSize = getOutputSize(matched);

            return outputStackSize >= 0;
        }

        /// <summary>
        /// Match a list of ingredients against the recipe and give back the amount that can be made with what's given
        /// Will return 0 if the ingredients are NOT in the right proportions!
        /// </summary>
        /// <param name="Inputs">a list of item stacks</param>
        /// <returns>the amount of the recipe that can be made</returns>
        public int Match(List<ItemStack> Inputs)
        {
            if (Inputs.Count != Ingredients.Length) //not the correct amount of ingredients for that recipe
                return 0;

            List<CraftingRecipeIngredient> matched = new List<CraftingRecipeIngredient>();
            int amountForTheRecipe = -1;

            foreach (ItemStack input in Inputs)
            {
                CraftingRecipeIngredient match = null;

                foreach (CraftingRecipeIngredient ing in Ingredients)   //check if this input item is in the recipe
                {
                    if (
                        (ing.ResolvedItemstack == null && !ing.IsWildCard)  //this ingredient is somehow null, but is not a wildcard
                        || matched.Contains(ing)                            //this ingredient is already matched 
                        || !ing.SatisfiesAsIngredient(input)                //the input does not match the ingredient
                    )
                        continue;

                    match = ing;                                            //otherwise the input satisfies as a an ingredient, no need to look further
                    break;
                }

                if (
                    match == null                                                           //didn't find a match for the input in previous step
                    || input.StackSize % match.Quantity != 0                                //this particular ingredient is not in enough quantity for full portions
                    || ((input.StackSize / match.Quantity) % Simmering.SmeltedRatio != 0)   //same but taking the smeltedRation into account ? would love to see an example where that's needed
                    )
                    return 0;

                int amountForThisIngredient = (input.StackSize / match.Quantity) / Simmering.SmeltedRatio;

                if (amountForThisIngredient > 0)    //the ingredient can at least produce a portion
                {
                    if (amountForTheRecipe == -1)   //first match
                    {
                        amountForTheRecipe = amountForThisIngredient;   //we set the target amount of portions
                    }

                    if (amountForThisIngredient == amountForTheRecipe)  //this ingredient matches the target amount, add it 
                    {
                        matched.Add(match);
                    }
                    else
                    {
                        return 0;   //we only want perfectly proportioned ingredients
                    }

                }
                else
                {
                    return 0;   //we need at least a full portion!
                }
            }

            return amountForTheRecipe;
        }

        List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> pairInput(ItemSlot[] inputStacks)
        {
            List<int> alreadyFound = new List<int>();

            Queue<ItemSlot> inputSlotsList = new Queue<ItemSlot>();
            foreach (var val in inputStacks) if (!val.Empty) inputSlotsList.Enqueue(val);

            if (inputSlotsList.Count != Ingredients.Length) return null;

            List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = new List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>>();

            while (inputSlotsList.Count > 0)
            {
                ItemSlot inputSlot = inputSlotsList.Dequeue();
                bool found = false;

                for (int i = 0; i < Ingredients.Length; i++)
                {

                    if (Ingredients[i].SatisfiesAsIngredient(inputSlot.Itemstack) && !alreadyFound.Contains(i))
                    {
                        matched.Add(new KeyValuePair<ItemSlot, CraftingRecipeIngredient>(inputSlot, Ingredients[i]));
                        alreadyFound.Add(i);
                        found = true;
                        break;
                    }
                }

                if (!found) return null;
            }

            // We're missing ingredients
            if (matched.Count != Ingredients.Length)
            {
                return null;
            }

            return matched;
        }


        int getOutputSize(List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched)
        {
            int outQuantityMul = -1;

            foreach (var val in matched)
            {
                ItemSlot inputSlot = val.Key;
                CraftingRecipeIngredient ingred = val.Value;
                int posChange = inputSlot.StackSize / ingred.Quantity;

                if (posChange < outQuantityMul || outQuantityMul == -1) outQuantityMul = posChange;
            }

            if (outQuantityMul == -1)
            {
                return -1;
            }


            foreach (var val in matched)
            {
                ItemSlot inputSlot = val.Key;
                CraftingRecipeIngredient ingred = val.Value;


                // Must have same or more than the total crafted amount
                if (inputSlot.StackSize < ingred.Quantity * outQuantityMul) return -1;

            }

            outQuantityMul = 1;
            return Simmering.SmeltedStack.StackSize * outQuantityMul;
        }

        public string GetOutputName()
        {
            return Lang.Get("aculinaryartillery:Will make {0}", Simmering.SmeltedStack.ResolvedItemstack.GetName());
        }

        public bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
        {
            bool ok = true;

            for (int i = 0; i < Ingredients.Length; i++)
            {
                ok &= Ingredients[i].Resolve(world, sourceForErrorLogging);
            }

            ok &= Simmering.SmeltedStack.Resolve(world, sourceForErrorLogging);

            return ok;
        }

        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(Code);
            writer.Write(Ingredients.Length);
            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i].ToBytes(writer);
            }
            writer.Write(this.Simmering.MeltingPoint);
            writer.Write(this.Simmering.MeltingDuration);
            writer.Write(this.Simmering.SmeltedRatio);
            writer.Write((ushort)this.Simmering.SmeltingType);
            CombustibleProperties simmering = this.Simmering;
            writer.Write(((simmering != null) ? simmering.SmeltedStack : null) != null);
            CombustibleProperties simmering2 = this.Simmering;
            if (((simmering2 != null) ? simmering2.SmeltedStack : null) != null)
            {
                this.Simmering.SmeltedStack.ToBytes(writer);
            }
            writer.Write(this.Simmering.RequiresContainer);
        }

        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            Code = reader.ReadString();
            Ingredients = new CraftingRecipeIngredient[reader.ReadInt32()];

            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i] = new CraftingRecipeIngredient();
                Ingredients[i].FromBytes(reader, resolver);
                Ingredients[i].Resolve(resolver, "Simmer Recipe (FromBytes)");
            }

            this.Simmering = new CombustibleProperties();
            this.Simmering.MeltingPoint = reader.ReadInt32();
            this.Simmering.MeltingDuration = reader.ReadSingle();
            this.Simmering.SmeltedRatio = reader.ReadInt32();
            this.Simmering.SmeltingType = (EnumSmeltType)reader.ReadUInt16();
            if (reader.ReadBoolean())
            {
                this.Simmering.SmeltedStack = new JsonItemStack();
                this.Simmering.SmeltedStack.FromBytes(reader, resolver.ClassRegistry);
                this.Simmering.SmeltedStack.Resolve(resolver, "Simmer Recipe (FromBytes)", true);
            }
            this.Simmering.RequiresContainer = reader.ReadBoolean();
        }

        public SimmerRecipe Clone()
        {
            CraftingRecipeIngredient[] ingredients = new CraftingRecipeIngredient[Ingredients.Length];
            for (int i = 0; i < Ingredients.Length; i++)
            {
                ingredients[i] = Ingredients[i].Clone();
            }

            return new SimmerRecipe()
            {
                Simmering = Simmering.Clone(),
                Code = Code,
                Enabled = Enabled,
                Name = Name,
                Ingredients = ingredients
            };
        }

        public Dictionary<string, string[]> GetNameToCodeMapping(IWorldAccessor world)
        {
            Dictionary<string, string[]> mappings = new Dictionary<string, string[]>();

            if (Ingredients == null || Ingredients.Length == 0) return mappings;

            foreach (var ingreds in Ingredients)
            {
                if (Ingredients.Length <= 0) continue;
                CraftingRecipeIngredient ingred = ingreds;
                if (ingred == null || !ingred.Code.Path.Contains("*") || ingred.Name == null) continue;

                int wildcardStartLen = ingred.Code.Path.IndexOf("*");
                int wildcardEndLen = ingred.Code.Path.Length - wildcardStartLen - 1;

                List<string> codes = new List<string>();

                if (ingred.Type == EnumItemClass.Block)
                {
                    for (int i = 0; i < world.Blocks.Count; i++)
                    {
                        if (world.Blocks[i].Code == null || world.Blocks[i].IsMissing) continue;

                        if (WildcardUtil.Match(ingred.Code, world.Blocks[i].Code))
                        {
                            string code = world.Blocks[i].Code.Path.Substring(wildcardStartLen);
                            string codepart = code.Substring(0, code.Length - wildcardEndLen);
                            if (ingred.AllowedVariants != null && !ingred.AllowedVariants.Contains(codepart)) continue;

                            codes.Add(codepart);

                        }
                    }
                }
                else
                {
                    for (int i = 0; i < world.Items.Count; i++)
                    {
                        if (world.Items[i].Code == null || world.Items[i].IsMissing) continue;

                        if (WildcardUtil.Match(ingred.Code, world.Items[i].Code))
                        {
                            string code = world.Items[i].Code.Path.Substring(wildcardStartLen);
                            string codepart = code.Substring(0, code.Length - wildcardEndLen);
                            if (ingred.AllowedVariants != null && !ingred.AllowedVariants.Contains(codepart)) continue;

                            codes.Add(codepart);
                        }
                    }
                }

                mappings[ingred.Name] = codes.ToArray();
            }

            return mappings;
        }
    }

    public class DoughIngredient : IByteSerializable
    {
        public CraftingRecipeIngredient[] Inputs;

        public CraftingRecipeIngredient GetMatch(ItemStack stack, bool checkStackSize = true)
        {
            if (stack == null) return null;

            for (int i = 0; i < Inputs.Length; i++)
            {
                if (Inputs[i].SatisfiesAsIngredient(stack, checkStackSize)) return Inputs[i];
            }

            return null;
        }

        public bool Resolve(IWorldAccessor world, string debug)
        {
            bool ok = true;

            for (int i = 0; i < Inputs.Length; i++)
            {
                ok &= Inputs[i].Resolve(world, debug);
            }

            return ok;
        }

        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            Inputs = new CraftingRecipeIngredient[reader.ReadInt32()];

            for (int i = 0; i < Inputs.Length; i++)
            {
                Inputs[i] = new CraftingRecipeIngredient();
                Inputs[i].FromBytes(reader, resolver);
                Inputs[i].Resolve(resolver, "Dough Ingredient (FromBytes)");
            }
        }

        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(Inputs.Length);
            for (int i = 0; i < Inputs.Length; i++)
            {
                Inputs[i].ToBytes(writer);
            }

        }

        public DoughIngredient Clone()
        {
            CraftingRecipeIngredient[] newings = new CraftingRecipeIngredient[Inputs.Length];

            for (int i = 0; i < Inputs.Length; i++)
            {
                newings[i] = Inputs[i].Clone();
            }

            return new DoughIngredient()
            {
                Inputs = newings
            };
        }
    }

    public class ACARecipeRegistrySystem : ModSystem
    {
        public static bool canRegister = true;

        public List<CookingRecipe> MixingRecipes = new List<CookingRecipe>();

        public List<DoughRecipe> DoughRecipes = new List<DoughRecipe>();

        public List<SimmerRecipe> SimmerRecipes = new List<SimmerRecipe>();

        public override double ExecuteOrder()
        {
            return 1.0;
        }

        public override void StartPre(ICoreAPI api)
        {
            canRegister = true;
        }

        public override void Start(ICoreAPI api)
        {
            MixingRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<CookingRecipe>>("mixingrecipes").Recipes;
            DoughRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<DoughRecipe>>("doughrecipes").Recipes;
            SimmerRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<SimmerRecipe>>("simmerrecipes").Recipes;
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            if (!(api is ICoreServerAPI coreServerAPI))
            {
                return;
            }
            loadMixingRecipes(coreServerAPI);
            loadDoughRecipes(coreServerAPI);
            loadSimmerRecipes(coreServerAPI);


        }
        void loadMixingRecipes(ICoreServerAPI coreServerAPI)
        {
            Dictionary<AssetLocation, JToken> files = coreServerAPI.Assets.GetMany<JToken>(coreServerAPI.Server.Logger, "recipes/mixing");
            int recipeQuantity = 0;

            foreach (var val in files)
            {
                String recCode = null;
                if (val.Value is JObject)
                {
                    CookingRecipe rec = val.Value.ToObject<CookingRecipe>();
                    if (!rec.Enabled) continue;

                    rec.Resolve(coreServerAPI.World, "mixing recipe " + val.Key);
                    RegisterCookingRecipe(rec);
                    recCode = rec.Code;
                    recipeQuantity++;
                }
                if (val.Value is JArray)
                {
                    foreach (var token in (val.Value as JArray))
                    {
                        CookingRecipe rec = token.ToObject<CookingRecipe>();
                        if (!rec.Enabled) continue;

                        rec.Resolve(coreServerAPI.World, "mixing recipe " + val.Key);
                        RegisterCookingRecipe(rec);
                        recCode = rec.Code;
                        recipeQuantity++;
                    }
                }
            }
            coreServerAPI.World.Logger.Event("{0} mixing recipes loaded", recipeQuantity);
            coreServerAPI.World.Logger.StoryEvent(Lang.Get("aculinaryartillery:The chef and the apprentice..."));
        }
        void loadDoughRecipes(ICoreServerAPI coreServerAPI)
        {
            Dictionary<AssetLocation, JToken> files = coreServerAPI.Assets.GetMany<JToken>(coreServerAPI.Server.Logger, "recipes/kneading");
            int recipeQuantity = 0;
            int ignored = 0;

            foreach (var val in files)
            {
                if (val.Value is JObject)
                {
                    DoughRecipe rec = val.Value.ToObject<DoughRecipe>();
                    if (!rec.Enabled) continue;

                    LoadKneadingRecipe(val.Key, rec, coreServerAPI, ref recipeQuantity, ref ignored);
                }
                if (val.Value is JArray)
                {
                    foreach (var token in (val.Value as JArray))
                    {
                        DoughRecipe rec = token.ToObject<DoughRecipe>();
                        if (!rec.Enabled) continue;

                        LoadKneadingRecipe(val.Key, rec, coreServerAPI, ref recipeQuantity, ref ignored);
                    }
                }
            }

            coreServerAPI.World.Logger.Event("{0} kneading recipes loaded", recipeQuantity);
            coreServerAPI.World.Logger.StoryEvent(Lang.Get("aculinaryartillery:The butter and the bread..."));
        }
        void loadSimmerRecipes(ICoreServerAPI coreServerAPI)
        {
            Dictionary<AssetLocation, JToken> files = coreServerAPI.Assets.GetMany<JToken>(coreServerAPI.Server.Logger, "recipes/simmering");
            int recipeQuantity = 0;
            int ignored = 0;

            foreach (var val in files)
            {
                if (val.Value is JObject)
                {
                    SimmerRecipe rec = val.Value.ToObject<SimmerRecipe>();
                    if (!rec.Enabled) continue;

                    LoadSimmeringRecipe(val.Key, rec, coreServerAPI, ref recipeQuantity, ref ignored);
                }
                if (val.Value is JArray)
                {
                    foreach (var token in (val.Value as JArray))
                    {
                        SimmerRecipe rec = token.ToObject<SimmerRecipe>();
                        if (!rec.Enabled) continue;

                        LoadSimmeringRecipe(val.Key, rec, coreServerAPI, ref recipeQuantity, ref ignored);
                    }
                }
            }
            coreServerAPI.World.Logger.Event("{0} simmer recipes loaded", recipeQuantity);
            coreServerAPI.World.Logger.StoryEvent(Lang.Get("aculinaryartillery:The syrup and lard..."));
        }
        void LoadSimmeringRecipe(AssetLocation path, SimmerRecipe recipe, ICoreServerAPI coreServerAPI, ref int quantityRegistered, ref int quantityIgnored)
        {
            if (!recipe.Enabled) return;
            if (recipe.Name == null) recipe.Name = path;
            string className = "simmer recipe";

            Dictionary<string, string[]> nameToCodeMapping = recipe.GetNameToCodeMapping(coreServerAPI.World);

            if (nameToCodeMapping.Count > 0)
            {
                List<SimmerRecipe> subRecipes = new List<SimmerRecipe>();

                int qCombs = 0;
                bool first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    if (first) qCombs = val2.Value.Length;
                    else qCombs *= val2.Value.Length;
                    first = false;
                }

                first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    string variantCode = val2.Key;
                    string[] variants = val2.Value;

                    for (int i = 0; i < qCombs; i++)
                    {
                        SimmerRecipe rec;

                        if (first) subRecipes.Add(rec = recipe.Clone());
                        else rec = subRecipes[i];

                        if (rec.Ingredients != null)
                        {
                            foreach (var ingreds in rec.Ingredients)
                            {
                                if (rec.Ingredients.Length <= 0) continue;
                                CraftingRecipeIngredient ingred = ingreds;

                                if (ingred.Name == variantCode)
                                {
                                    ingred.Code = ingred.Code.CopyWithPath(ingred.Code.Path.Replace("*", variants[i % variants.Length]));
                                }
                            }
                        }

                        rec.Simmering.SmeltedStack.FillPlaceHolder(val2.Key, variants[i % variants.Length]);
                    }

                    first = false;
                }

                if (subRecipes.Count == 0)
                {
                    coreServerAPI.World.Logger.Warning("{1} file {0} make uses of wildcards, but no blocks or item matching those wildcards were found.", path, className);
                }

                foreach (SimmerRecipe subRecipe in subRecipes)
                {
                    if (!subRecipe.Resolve(coreServerAPI.World, className + " " + path))
                    {
                        quantityIgnored++;
                        continue;
                    }
                    RegisterSimmerRecipe(subRecipe);
                    quantityRegistered++;
                }

            }
            else
            {
                if (!recipe.Resolve(coreServerAPI.World, className + " " + path))
                {
                    quantityIgnored++;
                    return;
                }

                RegisterSimmerRecipe(recipe);
                quantityRegistered++;
            }
        }
        void LoadKneadingRecipe(AssetLocation path, DoughRecipe recipe, ICoreServerAPI coreServerAPI, ref int quantityRegistered, ref int quantityIgnored)
        {
            if (!recipe.Enabled) return;
            if (recipe.Name == null) recipe.Name = path;
            string className = "kneading recipe";

            Dictionary<string, string[]> nameToCodeMapping = recipe.GetNameToCodeMapping(coreServerAPI.World);

            if (nameToCodeMapping.Count > 0)
            {
                List<DoughRecipe> subRecipes = new List<DoughRecipe>();

                int qCombs = 0;
                bool first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    if (first) qCombs = val2.Value.Length;
                    else qCombs *= val2.Value.Length;
                    first = false;
                }

                first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    string variantCode = val2.Key;
                    string[] variants = val2.Value;

                    for (int i = 0; i < qCombs; i++)
                    {
                        DoughRecipe rec;

                        if (first) subRecipes.Add(rec = recipe.Clone());
                        else rec = subRecipes[i];

                        if (rec.Ingredients != null)
                        {
                            foreach (var ingreds in rec.Ingredients)
                            {
                                if (ingreds.Inputs.Length <= 0) continue;
                                CraftingRecipeIngredient ingred = ingreds.Inputs[0];

                                if (ingred.Name == variantCode)
                                {
                                    ingred.Code = ingred.Code.CopyWithPath(ingred.Code.Path.Replace("*", variants[i % variants.Length]));
                                }
                            }
                        }

                        rec.Output.FillPlaceHolder(val2.Key, variants[i % variants.Length]);
                    }

                    first = false;
                }

                if (subRecipes.Count == 0)
                {
                    coreServerAPI.World.Logger.Warning("{1} file {0} make uses of wildcards, but no blocks or item matching those wildcards were found.", path, className);
                }

                foreach (DoughRecipe subRecipe in subRecipes)
                {
                    if (!subRecipe.Resolve(coreServerAPI.World, className + " " + path))
                    {
                        quantityIgnored++;
                        continue;
                    }
                    RegisterDoughRecipe(subRecipe);
                    quantityRegistered++;
                }

            }
            else
            {
                if (!recipe.Resolve(coreServerAPI.World, className + " " + path))
                {
                    quantityIgnored++;
                    return;
                }

                RegisterDoughRecipe(recipe);
                quantityRegistered++;
            }
        }
        public void RegisterCookingRecipe(CookingRecipe cookingrecipe)
        {
            if (!canRegister)
            {
                throw new InvalidOperationException("Coding error: Can no long register cooking recipes. Register them during AssetsLoad/AssetsFinalize and with ExecuteOrder < 99999");
            }

            MixingRecipes.Add(cookingrecipe);
        }
        public void RegisterDoughRecipe(DoughRecipe doughRecipe)
        {
            if (!canRegister)
            {
                throw new InvalidOperationException("Coding error: Can no long register dough recipes. Register them during AssetsLoad/AssetsFinalize and with ExecuteOrder < 99999");
            }

            DoughRecipes.Add(doughRecipe);
        }
        public void RegisterSimmerRecipe(SimmerRecipe simmerRecipe)
        {
            if (!canRegister)
            {
                throw new InvalidOperationException("Coding error: Can no long register simmering recipes. Register them during AssetsLoad/AssetsFinalize and with ExecuteOrder < 99999");
            }

            SimmerRecipes.Add(simmerRecipe);
        }


    }
}
