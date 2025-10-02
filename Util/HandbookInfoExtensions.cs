using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace ACulinaryArtillery.Util
{
    public static class HandbookInfoExtensions
    {
        public static List<RichTextComponentBase> ACAHandbookIngredientForComponents(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, ItemStack stack, Dictionary<string, Dictionary<CookingRecipeIngredient, HashSet<ItemStack?>>?> cachedValidStacks)
        {
            ItemStack maxstack = stack.Clone();
            maxstack.StackSize = maxstack.Collectible.MaxStackSize * 10; // because SatisfiesAsIngredient() tests for stacksize. Times 10 because liquid portion oddities

            List<ItemStack> recipestacks = [.. new HashSet<ItemStack>(capi.GetKneadingRecipes().Where(rec => rec.Ingredients.Any(ing => ing.GetMatch(maxstack) != null)).Select(rec => rec.Output.ResolvedItemstack).Where(stack => stack != null)),
                                            .. new HashSet<ItemStack>(capi.GetSimmerRecipes().Where(rec => rec.Ingredients.Any(ing => ing.SatisfiesAsIngredient(maxstack))).Select(rec => rec.Simmering.SmeltedStack.ResolvedItemstack).Where(stack => stack != null))];

            List<CookingRecipe> mixingrecipes = [.. capi.GetMixingRecipes().Where(recipe => recipe.CooksInto?.ResolvedItemstack == null && recipe.Ingredients!.Any(ingred => ingred.GetMatchingStack(stack) != null))];

            if (recipestacks.Count == 0 && mixingrecipes.Count == 0) return [];
            List<RichTextComponentBase> components = [];

            while (recipestacks.Count > 0)
            {
                ItemStack dstack = recipestacks[0];
                recipestacks.RemoveAt(0);
                if (dstack == null) continue;

                components.Add(new SlideshowItemstackTextComponent(capi, dstack, recipestacks, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
            }

            while (mixingrecipes.Count > 0)
            {
                CookingRecipe recipe;
                recipe = mixingrecipes[0];
                mixingrecipes.RemoveAt(0);
                if (recipe == null) continue;

                ItemStack mealBlock = new ItemStack(BlockMeal.RandomMealBowl(capi));
#nullable disable // This bit of the code will need to be changed once the proper caching is in vanilla
                var validStacks = cachedValidStacks.GetValueOrDefault(recipe.Code);
                components.Add(new MealstackTextComponent(capi, ref validStacks, mealBlock, recipe, 40, EnumFloat.Inline, allStacks, (cs) => openDetailPageFor("handbook-mealrecipe-" + recipe.Code), 6, false, maxstack));
                cachedValidStacks[recipe.Code] = validStacks;
#nullable restore
            }

            return components;
        }

        public static List<RichTextComponentBase> ACAHandbookCreatedByComponents(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, ItemStack stack)
        {
            DoughRecipe[] kneadingRecipes = [.. capi.GetKneadingRecipes().Where(rec => rec.Output.ResolvedItemstack.Satisfies(stack))];
            SimmerRecipe[] simmeringRecipes = [.. capi.GetSimmerRecipes().Where(rec => rec.Simmering.SmeltedStack.ResolvedItemstack.Satisfies(stack))];

            if (kneadingRecipes.Length == 0 && simmeringRecipes.Length == 0) return [];

            List<RichTextComponentBase> components = [];
            var verticalSpace = new ClearFloatTextComponent(capi, 7);

            if (kneadingRecipes.Length > 0)
            {
                CollectibleBehaviorHandbookTextAndExtraInfo.AddSubHeading(components, capi, openDetailPageFor, "Mixing", "craftinginfo-knapping");

                bool firstRecipe = true;
                foreach (var recipe in kneadingRecipes)
                {
                    if (recipe.Ingredients == null) continue;
                    if (!firstRecipe) components.Add(verticalSpace);
                    firstRecipe = false;

                    bool firstItem = true;
                    foreach (DoughIngredient ing in recipe.Ingredients)
                    {
                        ItemStack[] inputs = [.. ing.Inputs.Where(input => input.IsWildCard)
                                                           .SelectMany(input => capi.World.SearchItems(input.Code).Select(item => new ItemStack(item, input.Quantity))
                                                                                                                  .Where(stack => stack != null && ing.GetMatch(stack, false) != null)),
                                              .. ing.Inputs.Where(input => !input.IsWildCard && input.ResolvedItemstack != null)
                                                           .Select(input => new ItemStack(input.ResolvedItemstack.Id, input.ResolvedItemstack.Class, input.Quantity, (TreeAttribute)input.ResolvedItemstack.Attributes, capi.World))
                                                           .Where(stack => stack != null)
                                             ];

                        if (inputs.Length > 0)
                        {
                            if (!firstItem) components.Add(new RichTextComponent(capi, " + ", CairoFont.WhiteMediumText()) { VerticalAlign = EnumVerticalAlign.Middle });
                            components.Add(new SlideshowItemstackTextComponent(capi, inputs, 40, EnumFloat.Inline, (ItemStack cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))) { ShowStackSize = true, PaddingRight = 0 });
                            firstItem = false;
                        }
                    }

                    components.Add(new RichTextComponent(capi, " = ", CairoFont.WhiteMediumText()) { VerticalAlign = EnumVerticalAlign.Middle });
                    components.Add(new ItemstackTextComponent(capi, recipe.Output.ResolvedItemstack, 40, 10, EnumFloat.Inline) { ShowStacksize = true });
                }

                components.Add(verticalSpace);
            }

            if (simmeringRecipes.Length > 0)
            {
                CollectibleBehaviorHandbookTextAndExtraInfo.AddSubHeading(components, capi, openDetailPageFor, "Simmering", "craftinginfo-knapping");

                bool firstRecipe = true;
                foreach (var recipe in simmeringRecipes)
                {
                    if (recipe.Ingredients == null) continue;
                    if (!firstRecipe) components.Add(verticalSpace);
                    firstRecipe = false;

                    bool firstItem = true;
                    foreach (CraftingRecipeIngredient ing in recipe.Ingredients.Where(ing => ing.IsWildCard || ing.ResolvedItemstack != null))
                    {
                        ItemStack[] inputs = [.. ing.IsWildCard ? capi.World.SearchItems(ing.Code).Select(item => new ItemStack(item, ing.Quantity))
                                                                                                  .Where(stack => stack != null && ing.SatisfiesAsIngredient(stack, false)) :
                                              (ing.ResolvedItemstack != null ? [new ItemStack(ing.ResolvedItemstack.Id, ing.ResolvedItemstack.Class, ing.Quantity, (TreeAttribute)ing.ResolvedItemstack.Attributes, capi.World)] : [])
                                             ];

                        if (inputs.Length > 0)
                        {
                            if (!firstItem) components.Add(new RichTextComponent(capi, " + ", CairoFont.WhiteMediumText()) { VerticalAlign = EnumVerticalAlign.Middle });
                            components.Add(new SlideshowItemstackTextComponent(capi, inputs, 40, EnumFloat.Inline, (ItemStack cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))) { ShowStackSize = true, PaddingRight = 0 });
                            firstItem = false;
                        }
                    }

                    components.Add(new RichTextComponent(capi, " = ", CairoFont.WhiteMediumText()) { VerticalAlign = EnumVerticalAlign.Middle });
                    components.Add(new ItemstackTextComponent(capi, recipe.Simmering.SmeltedStack.ResolvedItemstack, 40, 10, EnumFloat.Inline) { ShowStacksize = true });
                }

                components.Add(verticalSpace);
            }

            return components;
        }

        public static List<RichTextComponentBase> ACAHandbookStorableComponents(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, ItemStack stack)
        {
            if (stack.ItemAttributes?["bottlerackable"].AsBool() != true) return [];
            List<ItemStack> displayStorables = [.. allStacks.Where(val => val.Collectible is BlockBottleRack)];

            if (displayStorables.Count == 0) return [];

            List<RichTextComponentBase> components = [];
            while (displayStorables.Count > 0)
            {
                ItemStack dstack = displayStorables[0];
                displayStorables.RemoveAt(0);
                if (dstack == null) continue;

                components.Add(new SlideshowItemstackTextComponent(capi, dstack, displayStorables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))) { PaddingLeft = 0 });
            }

            return components;
        }

        public static List<RichTextComponentBase> ACAHandbookStoredInComponents(ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, ItemStack stack)
        {
            if (stack.Collectible is not BlockBottleRack) return [];
            List<ItemStack> storables = [.. allStacks.Where(val => val.ItemAttributes?["bottlerackable"].AsBool() == true)];

            if (storables.Count == 0) return [];

            List<RichTextComponentBase> components = [];
            while (storables.Count > 0)
            {
                ItemStack dstack = storables[0];
                storables.RemoveAt(0);
                if (dstack == null) continue;

                SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, storables, 40, EnumFloat.Inline, (cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
                if (BlockLiquidContainerBase.GetContainableProps(dstack) is not WaterTightContainableProps) comp.ShowStackSize = true;
                comp.PaddingLeft = 0;
                components.Add(comp);
            }


            return components;
        }
    }
}