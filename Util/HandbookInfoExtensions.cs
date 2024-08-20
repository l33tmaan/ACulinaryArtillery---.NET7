using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using Vintagestory.API.Util;
using System.Runtime.CompilerServices;


namespace ACulinaryArtillery.Util
{
    public static class HandbookInfoExtensions
    {
        public static void addCreatedByMixingInfo(this List<RichTextComponentBase> components, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            List<DoughRecipe> doughRecipes = new List<DoughRecipe>();
            foreach (DoughRecipe doughRecipe in capi.GetKneadingRecipes())
            {
                if (doughRecipe.Output.ResolvedItemstack.Satisfies(inSlot.Itemstack))
                {
                    doughRecipes.Add(doughRecipe);
                }
            }
            if (doughRecipes.Count > 0)
            {
                ClearFloatTextComponent verticalSpaceSmall = new ClearFloatTextComponent(capi, 7f);
                ClearFloatTextComponent verticalSpace = new ClearFloatTextComponent(capi, 3f);
                components.Add(verticalSpaceSmall);
                RichTextComponent headc = new RichTextComponent(capi, Lang.Get("Created in: ") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold));
                components.Add(headc);
                ItemstackTextComponent minimixingcomp = new ItemstackTextComponent(capi, new ItemStack(capi.World.GetBlock(new AssetLocation("aculinaryartillery:mixingbowlmini"))), 80.0, 10, EnumFloat.Inline, delegate (ItemStack cs)
                {
                    openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs));
                });
                minimixingcomp.VerticalAlign = EnumVerticalAlign.Top;
                minimixingcomp.PaddingRight = 8.0;
                minimixingcomp.UnscaledMarginTop = 8.0;
                components.Add(minimixingcomp);
                Block[] poweredMixingBowlVariants = capi.World.SearchBlocks(new AssetLocation("aculinaryartillery:mixingbowl-*"));
                ItemStack[] poweredMixingBowlStacks = new ItemStack[0];
                Array.ForEach<Block>(poweredMixingBowlVariants, (Block block) => { poweredMixingBowlStacks = poweredMixingBowlStacks.Append(new ItemStack(block)); });
                SlideshowItemstackTextComponent poweredmixingcomp = new SlideshowItemstackTextComponent(capi, poweredMixingBowlStacks, 80.0, EnumFloat.Inline, delegate (ItemStack cs)
                {
                    openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs));
                });
                components.Add(poweredmixingcomp);
                /*
                components.Add(verticalSpace);
                components.Add(new LinkTextComponent(capi, Lang.Get("Mixing") + "\n", CairoFont.WhiteSmallText(), delegate
                {
                    openDetailPageFor("expandedfoodsguide2");
                }));
                */
                //OrderedDictionary<int, List<DoughRecipe>> grouped = new OrderedDictionary<int, List<DoughRecipe>>();
                ItemStack[] outputStacks = new ItemStack[doughRecipes.Count];

                int j = 0;
                foreach (DoughRecipe recipe in doughRecipes)
                {
                    outputStacks[j] = recipe.Output.ResolvedItemstack;

                    if (recipe.Ingredients == null) continue;
                    components.Add(verticalSpaceSmall);
                    components.Add(new RichTextComponent(capi, Lang.Get("Inputs: "), CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                    foreach (DoughIngredient ding in recipe.Ingredients)
                    {
                        ItemStack[] inputs = new ItemStack[0];
                        foreach (CraftingRecipeIngredient ting in ding.Inputs)
                        {
                            if (ting.IsWildCard)
                            {
                                Item[] matches = capi.World.SearchItems(ting.Code);

                                for (int i = 0; i < matches.Length; i++)
                                {
                                    ItemStack matchedStack = new ItemStack(matches[i]);

                                    if (matchedStack != null && ding.GetMatch(matchedStack, false) != null)
                                    {
                                        matchedStack.StackSize = ting.Quantity;
                                        inputs = inputs.Append(matchedStack.Clone());
                                    }
                                }
                            }
                            else if (ting.ResolvedItemstack != null)
                            {
                                ItemStack matchedStack = ting.ResolvedItemstack;
                                matchedStack.StackSize = ting.Quantity;
                                inputs = inputs.Append(matchedStack.Clone());
                            }

                        }
                        if (inputs.Length <= 0) { continue; }
                        SlideshowItemstackTextComponent incomp = new SlideshowItemstackTextComponent(capi, inputs, 40.0, EnumFloat.Inline, delegate (ItemStack cs)
                        {
                            openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs));
                        });
                        incomp.ShowStackSize = true;
                        incomp.PaddingRight = 5;
                        incomp.VerticalAlign = EnumVerticalAlign.FixedOffset;
                        incomp.UnscaledMarginTop = 10;
                        components.Add(incomp);
                    }
                }
                components.Add(new ClearFloatTextComponent(capi, 3f));
            }

        }
        public static void addMixingIngredientForInfo(this List<RichTextComponentBase> components, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            List<ItemStack> recipeOutputs = new List<ItemStack>();

            ItemStack maxstack = inSlot.Itemstack.Clone();
            maxstack.StackSize = maxstack.Collectible.MaxStackSize * 10;
            foreach (DoughRecipe doughRecipe in capi.GetKneadingRecipes())
            {
                foreach (DoughIngredient ing in doughRecipe.Ingredients)
                {
                    if (!recipeOutputs.Any((ItemStack s) => s.Equals(capi.World, doughRecipe.Output.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes)) && ing.GetMatch(maxstack) != null)
                    {
                        recipeOutputs.Add(doughRecipe.Output.ResolvedItemstack);
                    }
                }
            }
            if (recipeOutputs.Count() > 0)
            {
                ClearFloatTextComponent verticalSpaceSmall = new ClearFloatTextComponent(capi, 7f);
                ClearFloatTextComponent verticalSpace = new ClearFloatTextComponent(capi, 3f);
                components.Add(verticalSpaceSmall);
                RichTextComponent headc = new RichTextComponent(capi, Lang.Get("Kneading Ingredient for: ") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold));
                components.Add(headc);
                components.Add(new ClearFloatTextComponent(capi, 2f));
                while (recipeOutputs.Count() > 0)
                {
                    ItemStack dstack = recipeOutputs[0];
                    recipeOutputs.RemoveAt(0);
                    if (dstack != null)
                    {
                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, recipeOutputs, 40.0, EnumFloat.Inline, delegate (ItemStack cs)
                        {
                            openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs));
                        });
                        components.Add(comp);
                    }
                }
                components.Add(new ClearFloatTextComponent(capi, 3f));
            }
        }
        public static void addSimmerIngredientForInfo(this List<RichTextComponentBase> components, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            List<ItemStack> recipeOutputs = new List<ItemStack>();
            ItemStack maxstack = inSlot.Itemstack.Clone();
            maxstack.StackSize = maxstack.Collectible.MaxStackSize * 10;
            foreach (SimmerRecipe simmerRecipe in capi.GetSimmerRecipes())
            {
                foreach (CraftingRecipeIngredient ing in simmerRecipe.Ingredients)
                {
                    if (!recipeOutputs.Any((ItemStack s) => s.Equals(capi.World, simmerRecipe.Simmering.SmeltedStack.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes)) && ing.SatisfiesAsIngredient(maxstack))
                    {
                        recipeOutputs.Add(simmerRecipe.Simmering.SmeltedStack.ResolvedItemstack);
                    }
                }
            }
            if (recipeOutputs.Count() > 0)
            {
                ClearFloatTextComponent verticalSpaceSmall = new ClearFloatTextComponent(capi, 7f);
                ClearFloatTextComponent verticalSpace = new ClearFloatTextComponent(capi, 3f);
                components.Add(verticalSpaceSmall);
                RichTextComponent headc = new RichTextComponent(capi, Lang.Get("Simmering Ingredient for: ") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold));
                components.Add(headc);
                components.Add(new ClearFloatTextComponent(capi, 2f));
                while (recipeOutputs.Count() > 0)
                {
                    ItemStack dstack = recipeOutputs[0];
                    recipeOutputs.RemoveAt(0);
                    if (dstack != null)
                    {
                        SlideshowItemstackTextComponent comp = new SlideshowItemstackTextComponent(capi, dstack, recipeOutputs, 40.0, EnumFloat.Inline, delegate (ItemStack cs)
                        {
                            openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs));
                        });
                        components.Add(comp);
                    }
                }
                components.Add(new ClearFloatTextComponent(capi, 3f));
            }
        }
        public static void addCreatedBySimmeringInfo(this List<RichTextComponentBase> components, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            List<SimmerRecipe> simmerRecipes = new List<SimmerRecipe>();
            foreach (SimmerRecipe simmerRecipe in capi.GetSimmerRecipes())
            {
                if (simmerRecipe.Simmering.SmeltedStack.ResolvedItemstack.Satisfies(inSlot.Itemstack))
                {
                    simmerRecipes.Add(simmerRecipe);
                }
            }
            if (simmerRecipes.Count > 0)
            {
                ClearFloatTextComponent verticalSpaceSmall = new ClearFloatTextComponent(capi, 7f);
                ClearFloatTextComponent verticalSpace = new ClearFloatTextComponent(capi, 3f);
                components.Add(verticalSpaceSmall);
                RichTextComponent headc = new RichTextComponent(capi, Lang.Get("Created in: ") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold));
                components.Add(headc);
                ItemstackTextComponent saucepancomp = new ItemstackTextComponent(capi, new ItemStack(capi.World.GetBlock(new AssetLocation("aculinaryartillery:saucepan-burned"))), 80.0, 10, EnumFloat.Inline, delegate (ItemStack cs)
                {
                    openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs));
                });
                saucepancomp.VerticalAlign = EnumVerticalAlign.Top;
                saucepancomp.PaddingRight = 8.0;
                saucepancomp.UnscaledMarginTop = 8.0;
                components.Add(saucepancomp);
                Block[] minicauldronVariants = capi.World.SearchBlocks(new AssetLocation("aculinaryartillery:cauldronmini-*"));
                ItemStack[] minicauldronStacks = new ItemStack[0];
                Array.ForEach<Block>(minicauldronVariants, (Block block) => { minicauldronStacks = minicauldronStacks.Append(new ItemStack(block)); });
                SlideshowItemstackTextComponent minicauldroncomp = new SlideshowItemstackTextComponent(capi, minicauldronStacks, 80.0, EnumFloat.Inline, delegate (ItemStack cs)
                {
                    openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs));
                });
                components.Add(minicauldroncomp);
                Block[] cauldronVariants = capi.World.SearchBlocks(new AssetLocation("aculinaryartillery:cauldron-*"));
                ItemStack[] cauldronStacks = new ItemStack[0];
                Array.ForEach<Block>(cauldronVariants, (Block block) => { cauldronStacks = cauldronStacks.Append(new ItemStack(block)); });
                SlideshowItemstackTextComponent cauldroncomp = new SlideshowItemstackTextComponent(capi, cauldronStacks, 80.0, EnumFloat.Inline, delegate (ItemStack cs)
                {
                    openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs));
                });
                components.Add(cauldroncomp);

                /*
                components.Add(verticalSpace);
                components.Add(new LinkTextComponent(capi, Lang.Get("Mixing") + "\n", CairoFont.WhiteSmallText(), delegate
                {
                    openDetailPageFor("expandedfoodsguide2");
                }));
                */
                //OrderedDictionary<int, List<DoughRecipe>> grouped = new OrderedDictionary<int, List<DoughRecipe>>();
                ItemStack[] outputStacks = new ItemStack[simmerRecipes.Count];

                int j = 0;
                foreach (SimmerRecipe recipe in simmerRecipes)
                {
                    outputStacks[j] = recipe.Simmering.SmeltedStack.ResolvedItemstack;

                    if (recipe.Ingredients == null) continue;
                    components.Add(verticalSpaceSmall);
                    components.Add(new RichTextComponent(capi, Lang.Get("Inputs: "), CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                    
                    foreach (CraftingRecipeIngredient ing in recipe.Ingredients)
                    {
                        ItemStack[] inputs = new ItemStack[0];
                        if (ing.IsWildCard)
                        {
                            Item[] matches = capi.World.SearchItems(ing.Code);

                            for (int i = 0; i < matches.Length; i++)
                            {
                                ItemStack matchedStack = new ItemStack(matches[i]);
                                if (matchedStack != null && ing.SatisfiesAsIngredient(matchedStack, false))
                                {
                                    matchedStack.StackSize = ing.Quantity;
                                    inputs = inputs.Append(matchedStack.Clone());
                                }
                            }
                        }
                        else if (ing.ResolvedItemstack != null)
                        {
                            ItemStack matchedStack = ing.ResolvedItemstack;
                            matchedStack.StackSize = ing.Quantity;
                            inputs = inputs.Append(matchedStack.Clone());
                        }
                        if(inputs.Length <= 0) { continue; }
                        SlideshowItemstackTextComponent incomp = new SlideshowItemstackTextComponent(capi, inputs, 40.0, EnumFloat.Inline, delegate (ItemStack cs)
                        {
                            openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs));
                        });
                        incomp.ShowStackSize = true;
                        incomp.PaddingRight = 5;
                        incomp.VerticalAlign = EnumVerticalAlign.FixedOffset;
                        incomp.UnscaledMarginTop = 10;
                        components.Add(incomp);
                    }
                }
                components.Add(new ClearFloatTextComponent(capi, 3f));
            }
        }
    }
}
