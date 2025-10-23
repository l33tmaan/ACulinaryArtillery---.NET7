using ACulinaryArtillery.Util;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    [HarmonyPatch(typeof(BlockLiquidContainerBase))]
    public static class AntiCorkTransmutationPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(BlockLiquidContainerBase.SplitStackAndPerformAction))]
        public static bool TransmutationFix(ref BlockLiquidContainerBase __instance, ref int __result, Entity byEntity, ItemSlot slot, System.Func<ItemStack, int> action)
        {
            __result = BottleSplitStackAndPerformAction(byEntity, slot, action);
            return false;
        }
        public static int BottleSplitStackAndPerformAction(Entity byEntity, ItemSlot slot, System.Func<ItemStack, int> action)
        {
            if (slot.Itemstack == null)
            {
                return 0;
            }

            if (slot.Itemstack.StackSize == 1)
            {
                int num = action(slot.Itemstack);
                if (num > 0)
                {
                    _ = slot.Itemstack.Collectible.MaxStackSize;
                    EntityPlayer obj = byEntity as EntityPlayer;
                    if (obj == null)
                    {
                        return num;
                    }

                    obj.WalkInventory(delegate (ItemSlot pslot)
                    {
                        if (pslot.Empty || pslot is ItemSlotCreative || pslot.StackSize == pslot.Itemstack.Collectible.MaxStackSize || pslot.Itemstack.Item == byEntity.World.GetItem("aculinaryartillery:cork-generic"))
                        {
                            return true;
                        }

                        int mergableQuantity = slot.Itemstack.Collectible.GetMergableQuantity(slot.Itemstack, pslot.Itemstack, EnumMergePriority.DirectMerge);
                        if (mergableQuantity == 0)
                        {
                            return true;
                        }

                        BlockLiquidContainerBase obj3 = slot.Itemstack.Collectible as BlockLiquidContainerBase;
                        BlockLiquidContainerBase blockLiquidContainerBase = pslot.Itemstack.Collectible as BlockLiquidContainerBase;
                        if ((obj3?.GetContent(slot.Itemstack)?.StackSize).GetValueOrDefault() != (blockLiquidContainerBase?.GetContent(pslot.Itemstack)?.StackSize).GetValueOrDefault())
                        {
                            return true;
                        }

                        slot.Itemstack.StackSize += mergableQuantity;
                        pslot.TakeOut(mergableQuantity);
                        slot.MarkDirty();
                        pslot.MarkDirty();
                        return true;
                    });
                }

                return num;
            }

            ItemStack itemStack = slot.Itemstack.Clone();
            itemStack.StackSize = 1;
            int num2 = action(itemStack);
            if (num2 > 0)
            {
                slot.TakeOut(1);
                EntityPlayer obj2 = byEntity as EntityPlayer;
                if (obj2 == null || !obj2.Player.InventoryManager.TryGiveItemstack(itemStack, slotNotifyEffect: true))
                {
                    obj2.World.SpawnItemEntity(itemStack, byEntity.SidedPos.XYZ);
                }

                slot.MarkDirty();
            }

            return num2;
        }

        [HarmonyPatch(typeof(BlockPie), "CreateRecipe")]
        public static class BlockPieLiquidRecipeFix
        {
            public static void Postfix(ref CookingRecipe __result, IWorldAccessor world, string code, List<ItemStack> doughs, List<ItemStack> fillings, List<ItemStack> crusts)
            {
                __result.Ingredients[1].PortionSizeLitres = 0.1f;
            }
        }

        [HarmonyPatch(typeof(CollectibleBehaviorHandbookTextAndExtraInfo), "addIngredientForInfo")]
        public static class GetHandbookIngredientForPatch
        {
            public static void Postfix(ref bool __result, ref Dictionary<string, Dictionary<CookingRecipeIngredient, HashSet<ItemStack>>> ___cachedValidStacks, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop, List<ItemStack> containers, List<ItemStack> fuels, List<ItemStack> molds, bool haveText)
            {
                var newComponents = HandbookInfoExtensions.ACAHandbookIngredientForComponents(capi, allStacks, openDetailPageFor, stack, ___cachedValidStacks!);
                if (newComponents.Count == 0) return;

                if (!components.Any(comp => (comp as RichTextComponent)?.DisplayText == Lang.Get("Ingredient for") + "\n"))
                {
                    CollectibleBehaviorHandbookTextAndExtraInfo.AddHeading(components, capi, "Ingredient for", ref __result);
                    components.Add(new ClearFloatTextComponent(capi, 2));
                    components.AddRange(newComponents);
                    components.Add(new ClearFloatTextComponent(capi, 3));
                }
                else
                {
                    var firstMealstack = components.FirstOrDefault(comp => comp is MealstackTextComponent);
                    int insertIndex = components.Count - 1;
                    if (firstMealstack != null) insertIndex = components.IndexOf(firstMealstack);
                    components.InsertRange(insertIndex, newComponents);
                }
            }
        }

        [HarmonyPatch(typeof(CollectibleBehaviorHandbookTextAndExtraInfo), "addCreatedByInfo")]
        public static class GetHandbookCreatedByPatch
        {
            public static void Postfix(ref bool __result, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop, List<ItemStack> containers, List<ItemStack> fuels, List<ItemStack> molds, bool haveText)
            {
                var newComponents = HandbookInfoExtensions.ACAHandbookCreatedByComponents(capi, allStacks, openDetailPageFor, stack);
                if (newComponents.Count == 0) return;

                if (!components.Any(comp => (comp as RichTextComponent)?.DisplayText == Lang.Get("Created by") + "\n"))
                {
                    CollectibleBehaviorHandbookTextAndExtraInfo.AddHeading(components, capi, "Created by", ref __result);
                    components.Add(new ClearFloatTextComponent(capi, 3));
                    newComponents.RemoveAt(newComponents.Count - 1);
                    components.AddRange(newComponents);
                }
                else
                {
                    var beforeSubheading = components.FirstOrDefault(comp => (comp as RichTextComponent)?.DisplayText == "• " + Lang.Get("Baking (in oven)") + "\n");
                    beforeSubheading ??= components.FirstOrDefault(comp => (comp as RichTextComponent)?.DisplayText == "• " + Lang.Get("handbook-createdby-potcooking") + "\n");
                    beforeSubheading ??= components.FirstOrDefault(comp => (comp as RichTextComponent)?.DisplayText == "• " + Lang.Get("Crafting") + "\n");
                    int insertIndex = components.Count - 1;
                    if (beforeSubheading != null) insertIndex = components.IndexOf(beforeSubheading);
                    components.InsertRange(insertIndex, newComponents);
                }
            }
        }

        [HarmonyPatch(typeof(CollectibleBehaviorHandbookTextAndExtraInfo), "addStorableInfo")]
        public static class GetHandbookStorablePatch
        {
            public static void Postfix(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop)
            {
                var newComponents = HandbookInfoExtensions.ACAHandbookStorableComponents(capi, allStacks, openDetailPageFor, stack);
                if (newComponents.Count == 0) return;

                if (!components.Any(comp => (comp as RichTextComponent)?.DisplayText == Lang.Get("Storable in/on") + "\n"))
                {
                    bool haveText = components.Count > 0;
                    CollectibleBehaviorHandbookTextAndExtraInfo.AddHeading(components, capi, "Storable in/on", ref haveText);
                    components.Add(new ClearFloatTextComponent(capi, 3));
                    CollectibleBehaviorHandbookTextAndExtraInfo.AddSubHeading(components, capi, openDetailPageFor, "handbook-storable-displaycontainers", null);
                    newComponents[0].PaddingLeft = 5;
                    components.AddRange(newComponents);
                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }
                else
                {
                    var displaySubheading = components.FirstOrDefault(comp => (comp as RichTextComponent)?.DisplayText == "• " + Lang.Get("handbook-storable-displaycontainers") + "\n");
                    var beforeSubheading = components.FirstOrDefault(comp => (comp as RichTextComponent)?.DisplayText == "• " + Lang.Get("handbook-storable-liquidcontainers") + "\n");
                    beforeSubheading ??= components.FirstOrDefault(comp => (comp as RichTextComponent)?.DisplayText == "• " + Lang.Get("handbook-storable-foodcontainers") + "\n");
                    beforeSubheading ??= components.FirstOrDefault(comp => (comp as RichTextComponent)?.DisplayText == "• " + Lang.Get("handbook-storable-animalhusbandry") + "\n");
                    int insertIndex = components.Count - 1;
                    if (beforeSubheading != null) insertIndex = components.IndexOf(beforeSubheading);
                    if (displaySubheading != null) components.InsertRange(insertIndex, newComponents);
                    else
                    {
                        List<RichTextComponentBase> subheadingComponents = [];
                        CollectibleBehaviorHandbookTextAndExtraInfo.AddSubHeading(subheadingComponents, capi, openDetailPageFor, "handbook-storable-displaycontainers", null);
                        newComponents[0].PaddingLeft = 5;
                        components.InsertRange(insertIndex, [.. subheadingComponents, .. newComponents]);
                        components.Insert(insertIndex, new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CollectibleBehaviorHandbookTextAndExtraInfo), "addStoredInInfo")]
        public static class GetHandbookStoredInPatch
        {
            public static void Postfix(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, ItemStack stack, List<RichTextComponentBase> components, float marginTop)
            {
                var newComponents = HandbookInfoExtensions.ACAHandbookStoredInComponents(capi, allStacks, openDetailPageFor, stack);
                if (newComponents.Count == 0) return;

                if (!components.Any(comp => (comp as RichTextComponent)?.DisplayText == Lang.Get("handbook-storedin") + "\n"))
                {
                    bool haveText = components.Count > 0;
                    components.Add(new ClearFloatTextComponent(capi, 7));
                    CollectibleBehaviorHandbookTextAndExtraInfo.AddHeading(components, capi, "handbook-storedin", ref haveText);
                    newComponents[0].PaddingLeft = 5;
                    components.AddRange(newComponents);
                    components.Add(new RichTextComponent(capi, "\n", CairoFont.WhiteSmallText()));
                }
                else components.InsertRange(components.Count - 1, newComponents);
            }
        }

        [HarmonyPatch(typeof(ModSystemSurvivalHandbook), "onCreatePagesAsync")]
        public static class ModSystemSurvivalHandbookPatch
        {
            public static void Postfix(ref List<GuiHandbookPage> __result, ModSystemSurvivalHandbook __instance, ref ICoreClientAPI ___capi, ref ItemStack[] ___allstacks)
            {
                var firstPie = __result.FirstOrDefault(comp => comp.PageCode.Contains("handbook-mealrecipe-") && comp.PageCode.Contains("-pie"));
                int insertIndex = __result.Count;
                if (firstPie != null) insertIndex = __result.IndexOf(firstPie);

                foreach (var recipe in ___capi.GetMixingRecipes())
                {
                    if (___capi.IsShuttingDown) break;
                    if (recipe.CooksInto == null)
                    {
                        GuiHandbookMealRecipePage elem = new GuiHandbookMealRecipePage(___capi, recipe, ___allstacks, 6)
                        {
                            Visible = true
                        };

                        __result.Insert(insertIndex, elem);
                        insertIndex++;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(InventorySmelting))]
        public class SmeltingInvPatches
        {
            [HarmonyPostfix]
            [HarmonyPatch("GetOutputText")]
            public static void displayFix(ref string? __result, InventorySmelting __instance)
            {
                if (__instance[1].Itemstack?.Collectible is BlockSaucepan)
                {
                    __result = (__instance[1].Itemstack.Collectible as BlockSaucepan)?.GetOutputText(__instance.Api.World, __instance);
                }
            }


            /// <summary>
            /// Turns the
            /// <code>
            ///     ...
            ///	    if (targetSlot == this.slots[1] && (stack.Collectible is BlockSmeltingContainer || stack.Collectible is BlockCookingContainer))
            ///	    {
            ///	        ...
            ///	    }  
            ///	    ...
            /// </code>
            /// block
            /// into
            /// <code>
            ///     ...
            ///	    if (targetSlot == this.slots[1] && (stack.Collectible is BlockSmeltingContainer || stack.Collectible is BlockSaucePan || stack.Collectible is BlockCookingContainer))
            ///	    {
            ///	        ...
            ///	    }  
            ///	    ...
            /// </code>
            /// to make saucepans/cauldrons prefer a firepit's input slot.
            /// </summary>
            /// 

            [HarmonyPrefix]
            [HarmonyPatch(typeof(InventorySmelting), nameof(InventorySmelting.GetSuitability))]
            public static bool Harmony_InventorySmelting_GetSuitability_Prefix(
                ItemSlot sourceSlot, ItemSlot targetSlot, ItemSlot[] ___slots, ref float __result)
            {
                var stack = sourceSlot.Itemstack;
                if (targetSlot == ___slots[1] && stack.Collectible is BlockSaucepan)
                {
                    __result = 2.2f;
                    return false;
                }
                return true;
            }
            // Thanks Apache!!!
        }

        [HarmonyPatch(typeof(CookingRecipe))]
        public class CookingRecipePatches
        {
            [HarmonyPrefix]
            [HarmonyPatch("GetOutputName")]
            public static bool recipeNameFix(IWorldAccessor worldForResolve, ItemStack[] inputStacks, ref string __result, CookingRecipe __instance)
            {
                bool rotten = inputStacks.Any((stack) => stack?.Collectible.Code.Path == "rot");
                if (rotten)
                {
                    __result = Lang.Get("Rotten Food");
                    return false;
                }

                if (CookingRecipe.NamingRegistry.TryGetValue(__instance.Code!, out ICookingRecipeNamingHelper? namer))
                {
                    __result = namer.GetNameForIngredients(worldForResolve, __instance.Code!, inputStacks);
                    return false;
                }

                __result = new ACACookingRecipeNames().GetNameForIngredients(worldForResolve, __instance.Code, inputStacks);
                return false;
            }
        }

        [HarmonyPatch(typeof(CookingRecipeIngredient))]
        public class CookingIngredientPatches
        {
            [HarmonyPrefix]
            [HarmonyPatch("GetMatchingStack")]
            public static bool displayFix(ItemStack inputStack, ref CookingRecipeStack? __result, CookingRecipeIngredient __instance)
            {
                if (inputStack == null)
                {
                    __result = null;
                    return false;
                }

                string[] ignoredStackAttributes = [.. GlobalConstants.IgnoredStackAttributes, "madeWith", "expandedSats", "timeFrozen"];
                for (int i = 0; i < __instance.ValidStacks.Length; i++)
                {
                    bool isWildCard = __instance.ValidStacks[i].Code.Path.Contains("*");
                    bool found =
                        (isWildCard && inputStack.Collectible.WildCardMatch(__instance.ValidStacks[i].Code))
                        || (!isWildCard && inputStack.Equals(__instance.world, __instance.ValidStacks[i].ResolvedItemstack, ignoredStackAttributes))
                        || (__instance.ValidStacks[i].CookedStack?.ResolvedItemstack is ItemStack cookedStack && inputStack.Equals(__instance.world, cookedStack, ignoredStackAttributes))
                    ;

                    if (found)
                    {
                        __result = __instance.ValidStacks[i];
                        return false;
                    }
                }

                __result = null;
                return false;
            }
        }

        [HarmonyPatch(typeof(BlockCookedContainerBase))]
        public class BlockMealContainerBasePatches
        {
            [HarmonyPostfix]
            [HarmonyPatch("GetCookingRecipe")]
            public static void recipeFix(ref CookingRecipe? __result, ItemStack containerStack, IWorldAccessor world, BlockCookedContainerBase __instance)
            {
                __result ??= world.Api.GetMixingRecipes().FirstOrDefault(rec => rec.Code == __instance.GetRecipeCode(world, containerStack));
            }

            [HarmonyPostfix]
            [HarmonyPatch("GetMealRecipe")]
            public static void mealFix(ref CookingRecipe? __result, ItemStack containerStack, IWorldAccessor world, BlockCookedContainerBase __instance)
            {
                __result ??= world.Api.GetMixingRecipes().FirstOrDefault(rec => rec.Code == __instance.GetRecipeCode(world, containerStack));
            }
        }

        [HarmonyPatch(typeof(BlockMeal))]
        public class BlockMealBowlBasePatches
        {
            [HarmonyPostfix]
            [HarmonyPatch("GetCookingRecipe")]
            public static void recipeFix(ref CookingRecipe? __result, ItemStack containerStack, IWorldAccessor world, BlockCookedContainerBase __instance)
            {
                __result ??= world.Api.GetMixingRecipes().FirstOrDefault(rec => rec.Code == __instance.GetRecipeCode(world, containerStack));
            }


            [HarmonyPrefix]
            [HarmonyPatch("GetContentNutritionProperties", typeof(IWorldAccessor), typeof(ItemSlot), typeof(ItemStack[]), typeof(EntityAgent), typeof(bool), typeof(float), typeof(float))]
            public static bool nutriFix(IWorldAccessor world, ItemSlot inSlot, ItemStack[] contentStacks, EntityAgent forEntity, ref FoodNutritionProperties[] __result, bool mulWithStacksize = false, float nutritionMul = 1, float healthMul = 1)
            {
                List<FoodNutritionProperties> props = new List<FoodNutritionProperties>();
                for (int i = 0; i < contentStacks.Length; i++)
                {
                    if (contentStacks[i] == null)
                        continue;
                    props.AddRange(ItemExpandedRawFood.GetExpandedContentNutritionProperties(
                                                                                                world,
                                                                                                inSlot,
                                                                                                contentStacks[i],
                                                                                                forEntity,
                                                                                                mulWithStacksize,
                                                                                                nutritionMul,
                                                                                                healthMul
                                                                                                ));
                }

                __result = [.. props];
                return false;
            }


            [HarmonyPrefix]
            [HarmonyPatch("GetContentNutritionFacts", typeof(IWorldAccessor), typeof(ItemSlot), typeof(ItemStack[]), typeof(EntityAgent), typeof(bool), typeof(float), typeof(float))]
            public static bool nutriFactsFix(IWorldAccessor world, ItemSlot inSlotorFirstSlot, ItemStack[] contentStacks, EntityAgent forEntity, ref string __result, bool mulWithStacksize = false, float nutritionMul = 1, float healthMul = 1)
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


        [HarmonyPatch(typeof(BlockEntityQuern))]
        public class BlockEntityQuernPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch("grindInput")]
            public static bool grindInputWIthInheritedAttributes(ref int ___nowOutputFace, BlockEntityQuern __instance)
            {

                ItemStack grindedStack = __instance.InputGrindProps.GroundStack.ResolvedItemstack.Clone();
                if (grindedStack.Collectible is IExpandedFood food) food.OnCreatedByGrinding(__instance.InputStack, grindedStack);
                else return true;

                if (__instance.OutputSlot.Itemstack == null)
                {
                    __instance.OutputSlot.Itemstack = grindedStack;
                }
                else
                {
                    if (__instance.OutputSlot.Itemstack.Collectible.GetMergableQuantity(__instance.OutputSlot.Itemstack, grindedStack, EnumMergePriority.AutoMerge) > 0)
                    {
                        __instance.OutputSlot.Itemstack.StackSize += grindedStack.StackSize;
                    }
                    else
                    {
                        BlockFacing face = BlockFacing.HORIZONTALS[___nowOutputFace];
                        ___nowOutputFace = (___nowOutputFace + 1) % 4;

                        Block block = __instance.Api.World.BlockAccessor.GetBlock(__instance.Pos.AddCopy(face));
                        if (block.Replaceable < 6000) return false;
                        __instance.Api.World.SpawnItemEntity(grindedStack, __instance.Pos.ToVec3d().Add(0.5 + face.Normalf.X * 0.7, 0.75, 0.5 + face.Normalf.Z * 0.7), new Vec3d(face.Normalf.X * 0.02f, 0, face.Normalf.Z * 0.02f));
                    }
                }

                __instance.InputSlot.TakeOut(1);
                __instance.InputSlot.MarkDirty();
                __instance.OutputSlot.MarkDirty();
                return false;
            }
        }

        [HarmonyPatch(typeof(BlockEntityPie))]
        public class BlockEntityPiePatch
        {
            [HarmonyPrefix]
            [HarmonyPatch("TryAddIngredientFrom")]
            public static bool mulitPie(ref bool __result, ref InventoryBase ___inv, BlockEntityPie __instance, ItemSlot slot, IPlayer? byPlayer = null)
            {
                ICoreClientAPI? capi = __instance.Api as ICoreClientAPI;
                ILiquidSource? container = slot.Itemstack.Collectible as ILiquidSource;
                ItemStack contentStack = container?.GetContent(slot.Itemstack) ?? slot.Itemstack;
                InPieProperties? pieProps = contentStack.ItemAttributes?["inPieProperties"]?.AsObject<InPieProperties?>(null, contentStack.Collectible.Code.Domain);

                if (pieProps == null)
                {
                    if (byPlayer != null) capi?.TriggerIngameError(__instance, "notpieable", Lang.Get("This item can not be added to pies"));
                    __result = false;
                    return false;
                }

                float totalPortions = contentStack.StackSize / (container != null ? 20 : 2);
                if (totalPortions < 1)
                {
                    if (byPlayer != null) capi?.TriggerIngameError(__instance, "notpieable", Lang.Get(container != null ? "Need at least 0.2L liquid" : "Need at least 2 items each"));
                    __result = false;
                    return false;
                }

                if (___inv[0].Itemstack.Block is not BlockPie pieBlock)
                {
                    __result = false;
                    return false;
                }

                ItemStack[] cStacks = pieBlock.GetContents(__instance.Api.World, ___inv[0].Itemstack);

                bool isFull = cStacks[1] != null && cStacks[2] != null && cStacks[3] != null && cStacks[4] != null;
                bool hasFilling = cStacks[1] != null || cStacks[2] != null || cStacks[3] != null || cStacks[4] != null;

                if (isFull)
                {
                    if (pieProps.PartType == EnumPiePartType.Crust)
                    {
                        if (cStacks[5] == null)
                        {
                            cStacks[5] = slot.TakeOut(2);
                            pieBlock.SetContents(___inv[0].Itemstack, cStacks);
                        }
                        else
                        {
                            ItemStack stack = ___inv[0].Itemstack;
                            stack = BlockPie.CycleTopCrustType(stack);
                        }
                        __result = true;
                        return false;
                    }
                    if (byPlayer != null) capi?.TriggerIngameError(__instance, "piefullfilling", Lang.Get("Can't add more filling - already completely filled pie"));
                    __result = false;
                    return false;
                }

                if (pieProps.PartType != EnumPiePartType.Filling)
                {
                    if (byPlayer != null) capi?.TriggerIngameError(__instance, "pieneedsfilling", Lang.Get("Need to add a filling next"));
                    __result = false;
                    return false;
                }


                if (!hasFilling)
                {

                    cStacks[1] = container != null ? container.TryTakeContent(slot.Itemstack, 20) : slot.TakeOut(2);
                    pieBlock.SetContents(___inv[0].Itemstack, cStacks);
                    __result = true;
                    return false;
                }

                EnumFoodCategory[] foodCats = [.. cStacks.Select(BlockPie.FillingFoodCategory)];

                ItemStack cstack = contentStack;
                EnumFoodCategory foodCat = BlockPie.FillingFoodCategory(contentStack);

                bool equal = true;
                bool foodCatEquals = true;

                for (int i = 1; equal && i < cStacks.Length - 1; i++)
                {
                    if (cstack == null) continue;

                    equal &= cStacks[i] == null || cstack.Equals(__instance.Api.World, cStacks[i], GlobalConstants.IgnoredStackAttributes);
                    foodCatEquals &= cStacks[i] == null || foodCats[i] == foodCat;

                    cstack = cStacks[i];
                    foodCat = foodCats[i];
                }

                int emptySlotIndex = 2 + (cStacks[2] != null ? 1 + (cStacks[3] != null ? 1 : 0) : 0);

                if (equal)
                {
                    cStacks[emptySlotIndex] = container != null ? container.TryTakeContent(slot.Itemstack, 20) : slot.TakeOut(2);
                    pieBlock.SetContents(___inv[0].Itemstack, cStacks);
                    __result = true;
                    return false;
                }

                if (cStacks[1]?.ItemAttributes?["inPieProperties"]?.AsObject<InPieProperties?>(null, cStacks[1].Collectible.Code.Domain)?.AllowMixing == false)
                {
                    if (byPlayer != null) capi?.TriggerIngameError(__instance, "piefullfilling", Lang.Get("You really want to mix these to ingredients?! That would taste horrible!"));
                    __result = false;
                    return false;
                }

                cStacks[emptySlotIndex] = container != null ? container.TryTakeContent(slot.Itemstack, 20) : slot.TakeOut(2);
                pieBlock.SetContents(___inv[0].Itemstack, cStacks);
                __result = true;
                return false;
            }
        }
    }
}