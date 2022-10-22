using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    [HarmonyPatch(typeof(InventorySmelting))]
    class SmeltingInvPatches
    {
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        [HarmonyPostfix]
        [HarmonyPatch("GetOutputText")]
        static void displayFix(ref string __result, InventorySmelting __instance)
        {
            if (__instance[1].Itemstack?.Collectible is BlockSaucepan)
            {
                __result = (__instance[1].Itemstack.Collectible as BlockSaucepan).GetOutputText(__instance.Api.World, __instance);
            }
        }
    }

    [HarmonyPatch(typeof(CookingRecipeIngredient))]
    class CookingIngredientPatches
    {
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        [HarmonyPrefix]
        [HarmonyPatch("GetMatchingStack")]
        static bool displayFix(ItemStack inputStack, ref CookingRecipeStack __result, CookingRecipeIngredient __instance)
        {
            if (inputStack == null)
            { __result = null; return false; }

            for (int i = 0; i < __instance.ValidStacks.Length; i++)
            {
                bool isWildCard = __instance.ValidStacks[i].Code.Path.Contains("*");
                bool found =
                    (isWildCard && inputStack.Collectible.WildCardMatch(__instance.ValidStacks[i].Code))
                    || (!isWildCard && inputStack.Equals(__instance.world, __instance.ValidStacks[i].ResolvedItemstack, GlobalConstants.IgnoredStackAttributes.Concat(new string[] { "madeWith", "expandedSats" }).ToArray()))
                    || (__instance.ValidStacks[i].CookedStack?.ResolvedItemstack != null && inputStack.Equals(__instance.world, __instance.ValidStacks[i].ResolvedItemstack, GlobalConstants.IgnoredStackAttributes.Concat(new string[] { "madeWith", "expandedSats" }).ToArray()))
                ;

                if (found)
                { __result = __instance.ValidStacks[i]; return false; }
            }


            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(BlockEntityShelf))]
    class ShelfPatches
    {
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        /*
        [HarmonyPrefix]
        [HarmonyPatch("genMesh")]
        static bool displayFix(ItemStack stack, int index, ref MeshData __result, BlockEntityShelf __instance, ref Item ___nowTesselatingItem, ref Matrixf ___mat)
        {
            if (stack.Collectible is ItemExpandedRawFood)
            {
                string[] ings = (stack.Attributes?["madeWith"] as StringArrayAttribute)?.value;
                if (ings == null || ings.Length <= 0) return true;

                ___nowTesselatingItem = stack.Item;


                __result = (stack.Collectible as ItemExpandedRawFood).GenMesh(__instance.Api as ICoreClientAPI, ings, __instance, new Vec3f(0, __instance.Block.Shape.rotateY, 0));
                __result.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.BlendNoCull);

                float x = ((index % 4) >= 2) ? 12 / 16f : 4 / 16f;
                float y = index >= 4 ? 10 / 16f : 2 / 16f;
                float z = (index % 2 == 0) ? 4 / 16f : 10 / 16f;

                Vec4f offset = ___mat.TransformVector(new Vec4f(x - 0.5f, y, z - 0.5f, 0));
                __result.Translate(offset.XYZ);

                return false;
            }
            if (stack.Collectible is BlockBottle && !stack.Collectible.Code.Path.Contains("clay"))
            {
                ItemStack content = (stack.Collectible as BlockBottle).GetContent(stack);
                if (content == null) return true;
                __result = (stack.Collectible as BlockBottle).GenMesh(__instance.Api as ICoreClientAPI, content);
                //__result.RenderPasses.Fill((short)EnumChunkRenderPass.BlendNoCull);

                float x = ((index % 4) >= 2) ? 12 / 16f : 4 / 16f;
                float y = index >= 4 ? 10 / 16f : 2 / 16f;
                float z = (index % 2 == 0) ? 4 / 16f : 10 / 16f;

                Vec4f offset = ___mat.TransformVector(new Vec4f(x - 0.5f, y, z - 0.5f, 0));
                __result.Translate(offset.XYZ);

                return false;
            }

                return true;
        }
        */

        [HarmonyPrefix]
        [HarmonyPatch("GetBlockInfo")]
        static bool descFix(IPlayer forPlayer, StringBuilder sb, ref BlockEntityShelf __instance)
        {
            var rate = __instance.GetPerishRate();
            sb.AppendLine(Lang.Get("Stored food perish speed: {0}x", Math.Round(rate, 2)));

            var ripenRate = GameMath.Clamp((1 - rate - 0.5f) * 3, 0, 1);
            if (ripenRate > 0)
            { sb.AppendLine("Suitable spot for food ripening."); }

            sb.AppendLine();
            var up = forPlayer.CurrentBlockSelection != null && forPlayer.CurrentBlockSelection.SelectionBoxIndex > 1;

            for (var j = 3; j >= 0; j--)
            {
                var i = j + (up ? 4 : 0);
                i ^= 2;   //Display shelf contents text for items from left-to-right, not right-to-left
                if (__instance.Inventory[i].Empty)
                { continue; }

                var stack = __instance.Inventory[i].Itemstack;
                if (stack.Collectible is BlockCrock)
                { sb.Append(__instance.CrockInfoCompact(__instance.Inventory[i])); }
                else if (stack.Collectible is BlockBottle)
                {
                    sb.Append("Bottle (");
                    (__instance.Inventory[i].Itemstack.Collectible as BlockBottle).GetContentInfo(__instance.Inventory[i], sb, __instance.Api.World);
                    sb.AppendLine(")");
                }
                else
                {
                    if (stack.Collectible.TransitionableProps != null && stack.Collectible.TransitionableProps.Length > 0)
                    { sb.Append(BlockEntityShelf.PerishableInfoCompact(__instance.Api, __instance.Inventory[i], ripenRate)); }
                    else
                    { sb.AppendLine(stack.GetName()); }
                }
            }
            return false;
        }
    }


    [HarmonyPatch(typeof(BlockEntityDisplay))]
    class DisplayPatches
    {
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        [HarmonyPrefix]
        [HarmonyPatch("genMesh")]
        static bool displayFix(ItemStack stack, BlockEntityDisplay __instance, ref MeshData __result)
        //static bool displayFix(ItemStack stack, ref MeshData __result, BlockEntityDisplay __instance, ref Item ___nowTesselatingItem)
        {
            if (!(stack.Collectible is ItemExpandedRawFood))
                return true;
            string[] ings = (stack.Attributes?["madeWith"] as StringArrayAttribute)?.value;
            if (ings == null || ings.Length <= 0)
                return true;

            //___nowTesselatingItem = stack.Item;

            __result = (stack.Collectible as ItemExpandedRawFood).GenMesh(__instance.Api as ICoreClientAPI, ings, stack, new Vec3f(0, __instance.Block.Shape.rotateY, 0));
            //__result = (stack.Collectible as ItemExpandedRawFood).GenMesh(__instance.Api as ICoreClientAPI, ings, __instance, new Vec3f(0, __instance.Block.Shape.rotateY, 0));
            if (__result != null)
                __result.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.BlendNoCull);
            else
                return true;


            if (stack.Collectible.Attributes?[__instance.AttributeTransformCode].Exists == true)
            {
                ModelTransform transform = stack.Collectible.Attributes?[__instance.AttributeTransformCode].AsObject<ModelTransform>();
                transform.EnsureDefaultValues();
                transform.Rotation.Y += __instance.Block.Shape.rotateY;
                __result.ModelTransform(transform);
            }

            //if (__instance.Block.Shape.rotateY == 90 || __instance.Block.Shape.rotateY == 270) __result.Rotate(new Vec3f(0f, 0f, 0f), 0f, 90 * GameMath.DEG2RAD, 0f);

            return false;
        }
    }



    [HarmonyPatch(typeof(BlockEntityCookedContainer))]
    class BECookedContainerPatches
    {
        //This is for the cooking pot entity
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        [HarmonyPostfix]
        [HarmonyPatch("BlockEntityCookedContainer", MethodType.Constructor)]
        static void invFix(ref InventoryGeneric ___inventory)
        {
            ___inventory = new InventoryGeneric(6, null, null);
        }

        [HarmonyPostfix]
        [HarmonyPatch("FromRecipe", MethodType.Getter)]
        static void recipeFix(ref CookingRecipe __result, BlockEntityCookedContainer __instance)
        {
            if (__result == null)
                __result = MixingRecipeRegistry.Registry.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.RecipeCode);
        }

        [HarmonyPrefix]
        [HarmonyPatch("GetBlockInfo")]
        static bool infoFix(IPlayer forPlayer, ref StringBuilder dsc, BlockEntityCookedContainer __instance)
        {
            ItemStack[] contentStacks = __instance.GetNonEmptyContentStacks();
            CookingRecipe recipe = MixingRecipeRegistry.Registry.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.RecipeCode);
            if (recipe == null)
                return true;

            float servings = __instance.QuantityServings;
            int temp = (int)contentStacks[0].Collectible.GetTemperature(__instance.Api.World, contentStacks[0]);
            ;
            string temppretty = Lang.Get("{0}°C", temp);
            if (temp < 20)
                temppretty = "Cold";

            BlockMeal mealblock = __instance.Api.World.GetBlock(new AssetLocation("bowl-meal")) as BlockMeal;
            string nutriFacts = mealblock.GetContentNutritionFacts(__instance.Api.World, __instance.Inventory[0], contentStacks, forPlayer.Entity);


            if (servings == 1)
            {
                dsc.Append(Lang.Get("{0} serving of {1}\nTemperature: {2}{3}{4}", Math.Round(servings, 1), recipe.GetOutputName(forPlayer.Entity.World, contentStacks), temppretty, nutriFacts != null ? "\n" : "", nutriFacts));
            }
            else
            {
                dsc.Append(Lang.Get("{0} servings of {1}\nTemperature: {2}{3}{4}", Math.Round(servings, 1), recipe.GetOutputName(forPlayer.Entity.World, contentStacks), temppretty, nutriFacts != null ? "\n" : "", nutriFacts));
            }


            foreach (var slot in __instance.Inventory)
            {
                if (slot.Empty)
                    continue;

                TransitionableProperties[] propsm = slot.Itemstack.Collectible.GetTransitionableProperties(__instance.Api.World, slot.Itemstack, null);
                if (propsm != null && propsm.Length > 0)
                {
                    slot.Itemstack.Collectible.AppendPerishableInfoText(slot, dsc, __instance.Api.World);
                    break;
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(BlockEntityMeal))]
    class BEMealContainerPatches
    {
        //This is for the meal bowl entity
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        [HarmonyPostfix]
        [HarmonyPatch("BlockEntityMeal", MethodType.Constructor)]
        static void invFix(ref InventoryGeneric ___inventory)
        {
            ___inventory = new InventoryGeneric(6, null, null);
        }

        [HarmonyPostfix]
        [HarmonyPatch("FromRecipe", MethodType.Getter)]
        static void recipeFix(ref CookingRecipe __result, BlockEntityMeal __instance)
        {
            if (__result == null)
                __result = MixingRecipeRegistry.Registry.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.RecipeCode);
        }
    }

    [HarmonyPatch(typeof(BlockCookedContainerBase))]
    class BlockMealContainerBasePatches
    {
        //This is for the base food container
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        [HarmonyPostfix]
        [HarmonyPatch("GetCookingRecipe")]
        static void recipeFix(ref CookingRecipe __result, ItemStack containerStack, IWorldAccessor world, BlockCookedContainerBase __instance)
        {
            if (__result == null)
                __result = MixingRecipeRegistry.Registry.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.GetRecipeCode(world, containerStack));
        }

        [HarmonyPostfix]
        [HarmonyPatch("GetMealRecipe")]
        static void mealFix(ref CookingRecipe __result, ItemStack containerStack, IWorldAccessor world, BlockCookedContainerBase __instance)
        {
            if (__result == null)
                __result = MixingRecipeRegistry.Registry.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.GetRecipeCode(world, containerStack));
        }
    }

    [HarmonyPatch(typeof(BlockMeal))]
    class BlockMealBowlBasePatches
    {
        //This is for the food bowl block
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        [HarmonyPostfix]
        [HarmonyPatch("GetCookingRecipe")]
        static void recipeFix(ref CookingRecipe __result, ItemStack containerStack, IWorldAccessor world, BlockCookedContainerBase __instance)
        {
            if (__result == null)
                __result = MixingRecipeRegistry.Registry.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.GetRecipeCode(world, containerStack));
        }

        /*
         SPANG - March 13, 2022
         Don't call this shit
         Replaced with GetExpandedContentNutritionProperties in ItemExpandedRawFood.cs
        [HarmonyPrefix]
        [HarmonyPatch("GetContentNutritionProperties", typeof(IWorldAccessor), typeof(ItemSlot), typeof(ItemStack[]), typeof(EntityAgent), typeof(bool), typeof(float), typeof(float))]
        static bool nutriFix(IWorldAccessor world, ItemSlot inSlot, ItemStack[] contentStacks, EntityAgent forEntity, ref FoodNutritionProperties[] __result, bool mulWithStacksize = false, float nutritionMul = 1, float healthMul = 1)
        {
            List<FoodNutritionProperties> foodProps = new List<FoodNutritionProperties>();
            if (contentStacks == null)
                return true;

            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i] == null)
                    continue;

                CollectibleObject obj = contentStacks[i].Collectible;
                FoodNutritionProperties stackProps;

                if (obj.CombustibleProps != null && obj.CombustibleProps.SmeltedStack != null)
                {
                    stackProps = obj.CombustibleProps.SmeltedStack.ResolvedItemstack.Collectible.GetNutritionProperties(world, obj.CombustibleProps.SmeltedStack.ResolvedItemstack, forEntity);
                }
                else
                {
                    stackProps = obj.GetNutritionProperties(world, contentStacks[i], forEntity);
                }

                if (obj.Attributes?["nutritionPropsWhenInMeal"].Exists == true)
                {
                    stackProps = obj.Attributes?["nutritionPropsWhenInMeal"].AsObject<FoodNutritionProperties>();
                }

                if (stackProps == null)
                    continue;

                float mul = mulWithStacksize ? contentStacks[i].StackSize : 1;

                FoodNutritionProperties props = stackProps.Clone();

                DummySlot slot = new DummySlot(contentStacks[i], inSlot.Inventory);
                TransitionState state = contentStacks[i].Collectible.UpdateAndGetTransitionState(world, slot, EnumTransitionType.Perish);
                float spoilState = state != null ? state.TransitionLevel : 0;

                float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, forEntity);
                float healthLoss = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, forEntity);
                props.Satiety *= satLossMul * nutritionMul * mul;
                props.Health *= healthLoss * healthMul * mul;


                if (obj is ItemExpandedRawFood && (contentStacks[i].Attributes["expandedSats"] as FloatArrayAttribute)?.value?.Length == 6)
                {
                    FoodNutritionProperties[] exProps = (obj as ItemExpandedRawFood).GetPropsFromArray((contentStacks[i].Attributes["expandedSats"] as FloatArrayAttribute).value);

                    if (exProps == null || exProps.Length <= 0)
                        continue;

                    foreach (FoodNutritionProperties exProp in exProps)
                    {
                        exProp.Satiety *= satLossMul * mul * nutritionMul;
                        exProp.Health *= healthLoss * healthMul * mul;

                        foodProps.Add(exProp);
                    }
                }
                else
                {
                    foodProps.Add(props);
                }
            }

            __result = foodProps.ToArray();
            return false;
        }
        */

        [HarmonyPrefix]
        [HarmonyPatch("GetContentNutritionFacts", typeof(IWorldAccessor), typeof(ItemSlot), typeof(ItemStack[]), typeof(EntityAgent), typeof(bool), typeof(float), typeof(float))]
        static bool nutriFactsFix(IWorldAccessor world, ItemSlot inSlotorFirstSlot, ItemStack[] contentStacks, EntityAgent forEntity, ref string __result, bool mulWithStacksize = false, float nutritionMul = 1, float healthMul = 1)
        {
            FoodNutritionProperties[] props;

            Dictionary<EnumFoodCategory, float> totalSaturation = new Dictionary<EnumFoodCategory, float>();
            float totalHealth = 0;
            float satLossMul = 1;
            float healthLossMul = 1;

            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i] == null)
                    continue;
                DummySlot slot = new DummySlot(contentStacks[i], inSlotorFirstSlot.Inventory);
                TransitionState state = contentStacks[i].Collectible.UpdateAndGetTransitionState(world, slot, EnumTransitionType.Perish);
                float spoilState = state != null ? state.TransitionLevel : 0;

                satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, forEntity);
                healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, forEntity);

                props = ItemExpandedRawFood.GetExpandedContentNutritionProperties(world, inSlotorFirstSlot, contentStacks[i], forEntity, mulWithStacksize, nutritionMul, healthMul);
                for (int j = 0; j < props.Length; j++)
                {
                    FoodNutritionProperties prop = props[j];
                    if (prop == null)
                        continue;
                    float sat = 0;
                    totalSaturation.TryGetValue(prop.FoodCategory, out sat);
                    totalHealth += prop.Health * healthLossMul;
                    totalSaturation[prop.FoodCategory] = sat + prop.Satiety * satLossMul;
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Lang.Get("Nutrition Facts"));

            foreach (var val in totalSaturation)
            {
                sb.AppendLine("- " + Lang.Get("" + val.Key) + ": " + Math.Round(val.Value) + " sat.");
            }
            if (totalHealth != 0)
            {
                sb.AppendLine("- " + Lang.Get("Health: {0}{1} hp", totalHealth > 0 ? "+" : "", totalHealth));
            }

            __result = sb.ToString();
            return false;
        }
    }


    [HarmonyPatch(typeof(BlockCrock))]
    class BlockCrockContainerPatches
    {
        //This is for the cooking pot entity
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        [HarmonyPrefix]
        [HarmonyPatch("GetPlacedBlockInfo")]
        static bool infoFix(IWorldAccessor world, BlockPos pos, IPlayer forPlayer, BlockCrock __instance, ref string __result)
        {
            BlockEntityCrock becrock = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCrock;
            if (becrock == null)
                return true;

            BlockMeal mealblock = world.GetBlock(new AssetLocation("bowl-meal")) as BlockMeal;

            CookingRecipe recipe = MixingRecipeRegistry.Registry.MixingRecipes.FirstOrDefault((rec) => becrock.RecipeCode == rec.Code);
            ItemStack[] stacks = becrock.inventory.Where(slot => !slot.Empty).Select(slot => slot.Itemstack).ToArray();

            if (stacks == null || stacks.Length == 0)
            {
                return true;
            }

            StringBuilder dsc = new StringBuilder();

            if (recipe != null)
            {
                ItemSlot slot = BlockCrock.GetDummySlotForFirstPerishableStack(world, stacks, forPlayer.Entity, becrock.inventory);

                if (recipe != null)
                {
                    if (becrock.QuantityServings == 1)
                    {
                        dsc.AppendLine(Lang.Get("{0} serving of {1}", Math.Round(becrock.QuantityServings, 1), recipe.GetOutputName(world, stacks)));
                    }
                    else
                    {
                        dsc.AppendLine(Lang.Get("{0} servings of {1}", Math.Round(becrock.QuantityServings, 1), recipe.GetOutputName(world, stacks)));
                    }
                }

                string facts = mealblock.GetContentNutritionFacts(world, new DummySlot(__instance.OnPickBlock(world, pos)), null);

                if (facts != null)
                {
                    dsc.Append(facts);
                }

                slot.Itemstack?.Collectible.AppendPerishableInfoText(slot, dsc, world);
            }
            else
            {
                return true;
            }

            if (becrock.Sealed)
            {
                dsc.AppendLine("<font color=\"lightgreen\">" + Lang.Get("Sealed.") + "</font>");
            }


            __result = dsc.ToString();
            return false;
        }
    }

    [HarmonyPatch(typeof(BlockEntityPie))]
    class BlockEntityPiePatch
    {
        //This is for the pie entity
        //[HarmonyPrepare]
        //static bool Prepare()
        //{
        //    return true;
        //}

        [HarmonyPrefix]
        [HarmonyPatch("TryAddIngredientFrom")]
        static bool mulitPie(ref bool __result, BlockEntityPie __instance, ItemSlot slot, IPlayer byPlayer = null)
        {
            InventoryBase inv = __instance.Inventory;
            ICoreClientAPI capi = __instance.Api as ICoreClientAPI;

            var pieProps = slot.Itemstack.ItemAttributes?["inPieProperties"]?.AsObject<InPieProperties>(null, slot.Itemstack.Collectible.Code.Domain);
            if (pieProps == null)
            {
                if (byPlayer != null && capi != null)
                    capi.TriggerIngameError(__instance, "notpieable", Lang.Get("This item can not be added to pies"));
                __result = false;
                return false;
            }

            if (slot.StackSize < 2)
            {
                if (byPlayer != null && capi != null)
                    capi.TriggerIngameError(__instance, "notpieable", Lang.Get("Need at least 2 items each"));
                __result = false;
                return false;
            }

            var pieBlock = (inv[0].Itemstack.Block as BlockPie);
            if (pieBlock == null)
            { __result = false; return false; }

            ItemStack[] cStacks = pieBlock.GetContents(__instance.Api.World, inv[0].Itemstack);

            bool isFull = cStacks[1] != null && cStacks[2] != null && cStacks[3] != null && cStacks[4] != null;
            bool hasFilling = cStacks[1] != null || cStacks[2] != null || cStacks[3] != null || cStacks[4] != null;

            if (isFull)
            {
                if (pieProps.PartType == EnumPiePartType.Crust)
                {
                    if (cStacks[5] == null)
                    {
                        cStacks[5] = slot.TakeOut(2);
                        pieBlock.SetContents(inv[0].Itemstack, cStacks);
                    }
                    else
                    {
                        ItemStack stack = inv[0].Itemstack;
                        stack.Attributes.SetInt("topCrustType", (stack.Attributes.GetInt("topCrustType") + 1) % 3);
                    }
                    __result = true;
                    return false;
                }
                if (byPlayer != null && capi != null)
                    capi.TriggerIngameError(__instance, "piefullfilling", Lang.Get("Can't add more filling - already completely filled pie"));
                __result = false;
                return false;
            }

            if (pieProps.PartType != EnumPiePartType.Filling)
            {
                if (byPlayer != null && capi != null)
                    capi.TriggerIngameError(__instance, "pieneedsfilling", Lang.Get("Need to add a filling next"));
                __result = false;
                return false;
            }


            if (!hasFilling)
            {
                cStacks[1] = slot.TakeOut(2);
                pieBlock.SetContents(inv[0].Itemstack, cStacks);
                __result = true;
                return false;
            }

            var foodCats = cStacks.Select(stack => stack?.Collectible.NutritionProps?.FoodCategory ?? stack?.ItemAttributes?["nutritionPropsWhenInMeal"]?.AsObject<FoodNutritionProperties>()?.FoodCategory ?? EnumFoodCategory.Vegetable).ToArray();
            var stackprops = cStacks.Select(stack => stack?.ItemAttributes["inPieProperties"]?.AsObject<InPieProperties>(null, stack.Collectible.Code.Domain)).ToArray();

            ItemStack cstack = slot.Itemstack;
            EnumFoodCategory foodCat = slot.Itemstack?.Collectible.NutritionProps?.FoodCategory ?? slot.Itemstack?.ItemAttributes?["nutritionPropsWhenInMeal"]?.AsObject<FoodNutritionProperties>()?.FoodCategory ?? EnumFoodCategory.Vegetable;

            bool equal = true;
            bool foodCatEquals = true;

            for (int i = 1; equal && i < cStacks.Length - 1; i++)
            {
                if (cstack == null)
                    continue;

                equal &= cStacks[i] == null || cstack.Equals(__instance.Api.World, cStacks[i], GlobalConstants.IgnoredStackAttributes);
                foodCatEquals &= cStacks[i] == null || foodCats[i] == foodCat;

                cstack = cStacks[i];
                foodCat = foodCats[i];
            }

            int emptySlotIndex = 2 + (cStacks[2] != null ? 1 + (cStacks[3] != null ? 1 : 0) : 0);

            if (equal)
            {
                cStacks[emptySlotIndex] = slot.TakeOut(2);
                pieBlock.SetContents(inv[0].Itemstack, cStacks);
                __result = true;
                return false;
            }

            if (inv.Count < 0)
            {
                if (byPlayer != null && capi != null)
                    capi.TriggerIngameError(__instance, "piefullfilling", Lang.Get("Can't mix fillings from different food categories"));
                __result = false;
                return false;
            }
            else
            {
                if (!stackprops[1].AllowMixing)
                {
                    if (byPlayer != null && capi != null)
                        capi.TriggerIngameError(__instance, "piefullfilling", Lang.Get("You really want to mix these to ingredients?! That would taste horrible!"));
                    __result = false;
                    return false;
                }

                cStacks[emptySlotIndex] = slot.TakeOut(2);
                pieBlock.SetContents(inv[0].Itemstack, cStacks);
                __result = true;
                return false;
            }
        }
    }
}


