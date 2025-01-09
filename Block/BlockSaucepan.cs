using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using System.IO;
//using static ACulinaryArtillery.acaRecipeLoader;
using System.Linq;
using Vintagestory.ServerMods;

namespace ACulinaryArtillery
{
    public class BlockSaucepan : BlockLiquidContainerBase, ILiquidSource, ILiquidSink, IInFirepitRendererSupplier
    {
        public override bool CanDrinkFrom => true;
        public override bool IsTopOpened => true;
        public override bool AllowHeldLiquidTransfer => true;
        public AssetLocation liquidFillSoundLocation => new AssetLocation("game:sounds/effect/water-fill");

        private List<SimmerRecipe> simmerRecipes => api.GetSimmerRecipes();

        public bool isSealed;
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }
        public IInFirepitRenderer GetRendererWhenInFirepit(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return new SaucepanInFirepitRenderer(api as ICoreClientAPI, stack, firepit.Pos, forOutputSlot);
        }

        public EnumFirepitModel GetDesiredFirepitModel(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return EnumFirepitModel.Wide;
        }
        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            List<ItemStack> liquidContainerStacks = new List<ItemStack>();

            foreach (CollectibleObject obj in api.World.Collectibles)
            {
                if (obj is BlockLiquidContainerTopOpened || obj is ILiquidSource || obj is ILiquidSink || obj is BlockWateringCan)
                {
                    List<ItemStack> stacks = obj.GetHandBookStacks((ICoreClientAPI)api);
                    if (stacks != null) liquidContainerStacks.AddRange(stacks);
                }
            }

            return new WorldInteraction[]
                    {
                    new WorldInteraction()
                    {
                        ActionLangCode = "game:blockhelp-behavior-rightclickpickup",
                        MouseButton = EnumMouseButton.Right,
                        RequireFreeHand = true
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bucket-rightclick",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = liquidContainerStacks.ToArray()
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "aculinaryartillery:blockhelp-open", // json lang file. 
                        HotKeyCodes = new string[] { "sneak", "sprint" },
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) => {
                            BlockEntitySaucepan Besaucepan = world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntitySaucepan;
                            return Besaucepan != null && Besaucepan.isSealed;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "aculinaryartillery:blockhelp-close", // json lang file. 
                        HotKeyCodes = new string[] { "sneak", "sprint" },
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) => {
                            BlockEntitySaucepan Besaucepan = world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntitySaucepan;
                            return Besaucepan != null && !Besaucepan.isSealed;
                        }
                    }
            };
        }

        public override bool CanSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemStack inputStack, ItemStack outputStack)
        {
            //if there is something in the output, or if your saucepan already contains stuff in it, you can't cook
            if (outputStack != null || GetContent(inputStack) != null) return false;

            List<ItemStack> stacks = new List<ItemStack>();

            foreach (ItemSlot slot in cookingSlotsProvider.Slots)   //the cookingSlots are not necessarily filled in order. We just want the ones that are.
            {
                if (!slot.Empty) stacks.Add(slot.Itemstack.Clone());
            }

            if (stacks.Any())
            {
                //if it's just one stack, no need for an actual recipe, but we need to check the CombustibleProps 
                if (stacks.Count == 1)
                {
                    if (
                        (stacks[0].Collectible?.CombustibleProps?.SmeltedStack?.ResolvedItemstack != null)  //there is an output item defined and correctly resolved
                        && (stacks[0].Collectible?.CombustibleProps?.RequiresContainer ?? false)              //it requires a container
                        && (stacks[0].StackSize % stacks[0].Collectible.CombustibleProps.SmeltedRatio == 0) //there is a round number of items to smelt
                        )
                    {
                        return true;
                    }
                }
                else
                {
                    //SimmerRecipe match = null;
                    //int amountForTheseIngredients = 10;
                    if (simmerRecipes == null) return false;
                    foreach (SimmerRecipe rec in simmerRecipes)
                    {
                        if (rec.Match(stacks) >= 1)
                        {
                            return true;
                        }
                    }
                }
                    //return simmerRecipes.Any(); //otherwise, there are more than 1 items in the saucepan, so we only check that there are recipes, for now. We check whether they match the recipe in DoSmelt().
            }
            return false;
        }

        public override void DoSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot, ItemSlot outputSlot)
        {
            //if (!CanSmelt(world, cookingSlotsProvider, inputSlot.Itemstack, outputSlot.Itemstack))
             //   return;

            List<ItemStack> contents = new List<ItemStack>();   //The inputSlots may not all be filled. This is more convenient.
            ItemStack product = null;

            foreach (ItemSlot slot in cookingSlotsProvider.Slots)
            {
                if (!slot.Empty) contents.Add(slot.Itemstack);
            }

            if (contents.Count == 1)    //if there is only one ingredient, we have already checked it is adequate for smelting, so we immediately create the product using CombustibleProps
            {
                product = contents[0].Collectible.CombustibleProps.SmeltedStack.ResolvedItemstack.Clone();  //we create the unit output

                product.StackSize *= (contents[0].StackSize / contents[0].Collectible.CombustibleProps.SmeltedRatio);   //we multiply if there is enough for more than one unit output
            }
            else if (contents.Count > 1)
            {
                SimmerRecipe match = null;
                int amountForTheseIngredients = 10;

                foreach (SimmerRecipe rec in simmerRecipes)
                {
                    int amountForThisRecipe = rec.Match(contents);
                    if (amountForThisRecipe > 0)
                    {
                        match = rec;
                        amountForTheseIngredients = amountForThisRecipe;
                        break;
                    }
                }

                if (match == null) //none of the recipes matched
                    return;
                match.Simmering.SmeltedStack.Resolve(world, "Saucepansimmerrecipesmeltstack");
                product = match.Simmering.SmeltedStack.ResolvedItemstack.Clone();

                product.StackSize *= amountForTheseIngredients;

                //if the recipe produces something from Expanded Foods
                if (product.Collectible is IExpandedFood)
                {
                    List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> input = new List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>>();
                    List<ItemSlot> alreadyfound = new List<ItemSlot>();

                    foreach (CraftingRecipeIngredient ing in match.Ingredients) //for each ingredient in the recipe
                    {
                        foreach (ItemSlot slot in cookingSlotsProvider.Slots)
                        {
                            if (!alreadyfound.Contains(slot) && !slot.Empty && ing.SatisfiesAsIngredient(slot.Itemstack))
                            {
                                alreadyfound.Add(slot);
                                input.Add(new KeyValuePair<ItemSlot, CraftingRecipeIngredient>(slot, ing));
                                break;
                            }
                        }
                    }

                    (product.Collectible as IExpandedFood).OnCreatedByKneading(input, product);
                }
            }

            if (product == null)    //if we have no output to give
                return;
            //ACulinaryArtillery.logger.Debug("Product: " + product?.ToString());
           // ACulinaryArtillery.logger.Debug("Itemstack class: " + product?.Class.ToString());
            //ACulinaryArtillery.logger.Debug("Product class: " + product?.Collectible?.Class?.ToString());
           // ACulinaryArtillery.logger.Debug("Product: " + product.GetName() + " Is liquid?: " + (product.Collectible.Class == "ItemLiquidPortion" || product.Collectible is ItemExpandedLiquid || product.Collectible is ItemTransLiquid));
            if (product.Collectible.Class == "ItemLiquidPortion" || product.Collectible is ItemExpandedLiquid || product.Collectible is ItemTransLiquid)
            {
                for (int i = 0; i < cookingSlotsProvider.Slots.Length; i++)
                {
                    cookingSlotsProvider.Slots[i].Itemstack = null;
                }

                outputSlot.Itemstack = inputSlot.TakeOut(1);
                //ACulinaryArtillery.logger.Debug("OutputSlot: " + outputSlot.Itemstack.GetName());
                (outputSlot.Itemstack.Collectible as BlockLiquidContainerBase).TryPutLiquid(outputSlot.Itemstack, product, product.StackSize);

            }
            else
            {
                outputSlot.Itemstack = product;

                for (int i = 0; i < cookingSlotsProvider.Slots.Length; i++)
                {
                    cookingSlotsProvider.Slots[i].Itemstack = null;
                }

            }
        }

        public override float GetMeltingDuration(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
        {
            float dur = 0f;
            float speed = 10f;
            List<ItemStack> contents = new List<ItemStack>();
            foreach (ItemSlot slot in cookingSlotsProvider.Slots)
            {
                if (!slot.Empty) contents.Add(slot.Itemstack);
            }
            if (contents.Count == 1 && contents[0].Collectible.CombustibleProps != null) return contents[0].Collectible.CombustibleProps.MeltingDuration * contents[0].StackSize / speed;
            else if (simmerRecipes != null && contents.Count > 1)
            {
                SimmerRecipe match = null;
                int amount = 0;

                foreach (SimmerRecipe rec in simmerRecipes)
                {
                    if (rec.Match(contents) > 0)
                    {
                        match = rec;
                        amount = rec.Match(contents);
                        break;
                    }
                }

                if (match == null) return 0;

                return match.Simmering.MeltingDuration * amount / speed;
            }

            return dur;
        }

        public override float GetMeltingPoint(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
        {
            float temp = 0f;
            List<ItemStack> contents = new List<ItemStack>();
            foreach (ItemSlot slot in cookingSlotsProvider.Slots)
            {
                if (!slot.Empty) contents.Add(slot.Itemstack);
            }
            if (contents.Count == 1 && contents[0].Collectible.CombustibleProps != null) return contents[0].Collectible.CombustibleProps.MeltingPoint;
            else if (simmerRecipes != null && contents.Count > 1)
            {
                SimmerRecipe match = null;
                int amount = 0;

                foreach (SimmerRecipe rec in simmerRecipes)
                {
                    if (rec.Match(contents) > 0)
                    {
                        amount = rec.Match(contents);
                        match = rec;
                        break;
                    }
                }

                if (match == null) return 0;

                return match.Simmering.MeltingPoint;
            }

            return temp;
        }

        // We have overrides for TryPutLiquid, but these are almost carbon copies of the base method, wont remove *yet* incase we do want to write some custom behavior and the base code is a bit harder to read imo
        public override int TryPutLiquid(ItemStack containerStack, ItemStack liquidStack, float desiredLitres)
        {
            if (liquidStack == null) return 0;

            var props = GetContainableProps(liquidStack);
            if (props == null) return 0;

            int desiredItems = (int)(props.ItemsPerLitre * desiredLitres);
            int availItems = liquidStack.StackSize;

            ItemStack stack = GetContent(containerStack);
            ILiquidSink sink = containerStack.Collectible as ILiquidSink;

            if (stack == null)
            {
                if (!props.Containable) return 0;

                int placeableItems = (int)(sink.CapacityLitres * props.ItemsPerLitre);

                ItemStack placedstack = liquidStack.Clone();
                placedstack.StackSize = GameMath.Min(availItems, desiredItems, placeableItems);
                SetContent(containerStack, placedstack);

                return Math.Min(desiredItems, placeableItems);
            }
            else
            {
                if (!stack.Equals(api.World, liquidStack, GlobalConstants.IgnoredStackAttributes)) return 0;

                float maxItems = sink.CapacityLitres * props.ItemsPerLitre;
                int placeableItems = (int)(maxItems - (float)stack.StackSize);

                stack.StackSize += GameMath.Min(placeableItems, desiredItems, availItems);
                return Math.Min(placeableItems, desiredItems);
            }
        }

        public override int TryPutLiquid(BlockPos pos, ItemStack liquidStack, float desiredLitres)
        {
            if (liquidStack == null) return 0;

            var props = GetContainableProps(liquidStack);
            int desiredItems = (int)(props.ItemsPerLitre * desiredLitres);
            float availItems = liquidStack.StackSize;
            float maxItems = CapacityLitres * props.ItemsPerLitre;

            ItemStack stack = GetContent(pos);
            if (stack == null)
            {
                if (props == null || !props.Containable) return 0;

                int placeableItems = (int)GameMath.Min(desiredItems, maxItems, availItems);
                int movedItems = Math.Min(desiredItems, placeableItems);

                ItemStack placedstack = liquidStack.Clone();
                placedstack.StackSize = movedItems;
                SetContent(pos, placedstack);

                return movedItems;
            }
            else
            {
                if (!stack.Equals(api.World, liquidStack, GlobalConstants.IgnoredStackAttributes)) return 0;

                int placeableItems = (int)Math.Min(availItems, maxItems - (float)stack.StackSize);
                int movedItems = Math.Min(placeableItems, desiredItems);

                stack.StackSize += GameMath.Min(movedItems);
                api.World.BlockAccessor.GetBlockEntity(pos).MarkDirty(true);
                (api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer).Inventory[GetContainerSlotId(pos)].MarkDirty();

                return GameMath.Min(movedItems);
            }
        }

        public static WaterTightContainableProps GetInContainerProps(ItemStack stack)
        {
            try
            {
                JsonObject obj = stack?.ItemAttributes?["waterTightContainerProps"];
                if (obj != null && obj.Exists) return obj.AsObject<WaterTightContainableProps>(null, stack.Collectible.Code.Domain);
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {

            BlockEntitySaucepan sp = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySaucepan;
            BlockPos pos = blockSel.Position;

            if (byPlayer.WorldData.EntityControls.Sneak && byPlayer.WorldData.EntityControls.Sprint)
            {
                if (sp != null && Attributes.IsTrue("canSeal"))
                {
                    world.PlaySoundAt(AssetLocation.Create(Attributes["lidSound"].AsString("sounds/block"), Code.Domain), pos.X + 0.5f, pos.Y + 0.5f, pos.Z + 0.5f, byPlayer);
                    sp.isSealed = !sp.isSealed;
                    sp.RedoMesh();
                    sp.MarkDirty(true);
                }

                return true;
            }

            if (sp?.isSealed == true) return false;
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!hotbarSlot.Empty && hotbarSlot.Itemstack.Collectible.Attributes?.IsTrue("handleLiquidContainerInteract") == true)
            {
                EnumHandHandling handling = EnumHandHandling.NotHandled;
                hotbarSlot.Itemstack.Collectible.OnHeldInteractStart(hotbarSlot, byPlayer.Entity, blockSel, null, true, ref handling);
                if (handling == EnumHandHandling.PreventDefault || handling == EnumHandHandling.PreventDefaultAction) return true;
            }

            if (hotbarSlot.Empty || !(hotbarSlot.Itemstack.Collectible is ILiquidInterface)) return base.OnBlockInteractStart(world, byPlayer, blockSel);


            CollectibleObject obj = hotbarSlot.Itemstack.Collectible;

            bool singleTake = byPlayer.WorldData.EntityControls.Sneak;
            bool singlePut = byPlayer.WorldData.EntityControls.Sprint;

        /*    if (obj is ILiquidSource && !singleTake)
            {
                int moved = TryPutLiquid(blockSel.Position, (obj as ILiquidSource).GetContent(hotbarSlot.Itemstack), singlePut ? 1 : 9999);

                if (moved > 0)
                {
                    (obj as ILiquidSource).TryTakeContent(hotbarSlot.Itemstack, moved);
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                    return true;
                }
            }

            if (obj is ILiquidSink && !singlePut)
            {
                ItemStack owncontentStack = GetContent(blockSel.Position);
                int moved = 0;

                if (hotbarSlot.Itemstack.StackSize == 1)
                {
                    moved = (obj as ILiquidSink).TryPutLiquid(hotbarSlot.Itemstack, owncontentStack, singleTake ? 1 : 9999);
                }
                else
                {
                    ItemStack containerStack = hotbarSlot.Itemstack.Clone();
                    containerStack.StackSize = 1;
                    moved = (obj as ILiquidSink).TryPutLiquid(containerStack, owncontentStack, singleTake ? 1 : 9999);

                    if (moved > 0)
                    {
                        hotbarSlot.TakeOut(1);
                        if (!byPlayer.InventoryManager.TryGiveItemstack(containerStack, true))
                        {
                            api.World.SpawnItemEntity(containerStack, byPlayer.Entity.SidedPos.XYZ);
                        }
                    }
                }

                if (moved > 0)
                {
                    TryTakeContent(blockSel.Position, moved);
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    return true;
                }
            } */

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
   
        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (itemslot.Itemstack?.Attributes.GetBool("isSealed") == true) return;

            if (blockSel == null || byEntity.Controls.Sneak)
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            if (AllowHeldLiquidTransfer)
            {
                IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

                ItemStack contentStack = GetContent(itemslot.Itemstack);
                WaterTightContainableProps props = contentStack == null ? null : GetContentProps(contentStack);

                Block targetedBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

                if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
                {
                    byEntity.World.BlockAccessor.MarkBlockDirty(blockSel.Position.AddCopy(blockSel.Face));
                    byPlayer?.InventoryManager.ActiveHotbarSlot?.MarkDirty();
                    return;
                }

                if (!TryFillFromBlock(itemslot, byEntity, blockSel.Position))
                {
                    BlockLiquidContainerTopOpened targetCntBlock = targetedBlock as BlockLiquidContainerTopOpened;
                    if (targetCntBlock != null)
                    {
                        if (targetCntBlock.TryPutLiquid(blockSel.Position, contentStack, targetCntBlock.CapacityLitres) > 0)
                        {
                            TryTakeContent(itemslot.Itemstack, 1);
                            byEntity.World.PlaySoundAt(props.FillSpillSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                        }

                    }
                    else
                    {
                        if (byEntity.Controls.Sprint)
                        {
                            SpillContents(itemslot, byEntity, blockSel);
                        }
                    }
                }
            }

            if (CanDrinkFrom)
            {
                if (GetNutritionProperties(byEntity.World, itemslot.Itemstack, byEntity) != null)
                {
                    tryEatBegin(itemslot, byEntity, ref handHandling, "drink", 4);
                    return;
                }
            }

            if (AllowHeldLiquidTransfer || CanDrinkFrom)
            {
                // Prevent placing on normal use
                handHandling = EnumHandHandling.PreventDefaultAction;
            }

            /* IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

             if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
             {
                 byEntity.World.BlockAccessor.MarkBlockDirty(blockSel.Position.AddCopy(blockSel.Face));
                 byPlayer?.InventoryManager.ActiveHotbarSlot?.MarkDirty();
                 return;
             }

             // Prevent placing on normal use
             handHandling = EnumHandHandling.PreventDefaultAction;


             base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);*/
        }

        private bool SpillContents(ItemSlot containerSlot, EntityAgent byEntity, BlockSelection blockSel)
        {
            BlockPos pos = blockSel.Position;
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            IBlockAccessor blockAcc = byEntity.World.BlockAccessor;
            BlockPos secondPos = blockSel.Position.AddCopy(blockSel.Face);
            var contentStack = GetContent(containerSlot.Itemstack);

            WaterTightContainableProps props = GetContentProps(containerSlot.Itemstack);

            if (props == null || !props.AllowSpill || props.WhenSpilled == null) return false;

            if (!byEntity.World.Claims.TryAccess(byPlayer, secondPos, EnumBlockAccessFlags.BuildOrBreak))
            {
                return false;
            }

            var action = props.WhenSpilled.Action;
            float currentlitres = GetCurrentLitres(containerSlot.Itemstack);

            if (currentlitres > 0 && currentlitres < 10)
            {
                action = WaterTightContainableProps.EnumSpilledAction.DropContents;
            }

            if (action == WaterTightContainableProps.EnumSpilledAction.PlaceBlock)
            {
                Block waterBlock = byEntity.World.GetBlock(props.WhenSpilled.Stack.Code);

                if (props.WhenSpilled.StackByFillLevel != null)
                {
                    JsonItemStack fillLevelStack;
                    props.WhenSpilled.StackByFillLevel.TryGetValue((int)currentlitres, out fillLevelStack);
                    if (fillLevelStack != null) waterBlock = byEntity.World.GetBlock(fillLevelStack.Code);
                }

                Block currentblock = blockAcc.GetBlock(pos);
                if (currentblock.Replaceable >= 6000)
                {
                    blockAcc.SetBlock(waterBlock.BlockId, pos);
                    blockAcc.TriggerNeighbourBlockUpdate(pos);
                    blockAcc.MarkBlockDirty(pos);
                }
                else
                {
                    if (blockAcc.GetBlock(secondPos).Replaceable >= 6000)
                    {
                        blockAcc.SetBlock(waterBlock.BlockId, secondPos);
                        blockAcc.TriggerNeighbourBlockUpdate(pos);
                        blockAcc.MarkBlockDirty(secondPos);
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            if (action == WaterTightContainableProps.EnumSpilledAction.DropContents)
            {
                props.WhenSpilled.Stack.Resolve(byEntity.World, "liquidcontainerbasespill");

                ItemStack stack = props.WhenSpilled.Stack.ResolvedItemstack.Clone();
                stack.StackSize = contentStack.StackSize;

                byEntity.World.SpawnItemEntity(stack, blockSel.Position.ToVec3d().Add(blockSel.HitPosition));
            }


            int moved = splitStackAndPerformAction(byEntity, containerSlot, (stack) => { SetContent(stack, null); return contentStack.StackSize; });

            DoLiquidMovedEffects(byPlayer, contentStack, moved, EnumLiquidDirection.Pour);
            return true;
        }

        private int splitStackAndPerformAction(Entity byEntity, ItemSlot slot, System.Func<ItemStack, int> action)
        {
            if (slot.Itemstack == null) return 0;
            if (slot.Itemstack.StackSize == 1)
            {
                int moved = action(slot.Itemstack);

                if (moved > 0)
                {
                    int maxstacksize = slot.Itemstack.Collectible.MaxStackSize;

                    (byEntity as EntityPlayer)?.WalkInventory((pslot) =>
                    {
                        if (pslot.Empty || pslot is ItemSlotCreative || pslot.StackSize == pslot.Itemstack.Collectible.MaxStackSize) return true;
                        int mergableq = slot.Itemstack.Collectible.GetMergableQuantity(slot.Itemstack, pslot.Itemstack, EnumMergePriority.DirectMerge);
                        if (mergableq == 0) return true;

                        var selfLiqBlock = slot.Itemstack.Collectible as BlockLiquidContainerBase;
                        var invLiqBlock = pslot.Itemstack.Collectible as BlockLiquidContainerBase;

                        if ((selfLiqBlock?.GetContent(slot.Itemstack)?.StackSize ?? 0) != (invLiqBlock?.GetContent(pslot.Itemstack)?.StackSize ?? 0)) return true;

                        slot.Itemstack.StackSize += mergableq;
                        pslot.TakeOut(mergableq);

                        slot.MarkDirty();
                        pslot.MarkDirty();
                        return true;
                    });
                }

                return moved;
            }
            else
            {
                ItemStack containerStack = slot.Itemstack.Clone();
                containerStack.StackSize = 1;

                int moved = action(containerStack);

                if (moved > 0)
                {
                    slot.TakeOut(1);
                    if ((byEntity as EntityPlayer)?.Player.InventoryManager.TryGiveItemstack(containerStack, true) != true)
                    {
                        api.World.SpawnItemEntity(containerStack, byEntity.SidedPos.XYZ);
                    }

                    slot.MarkDirty();
                }

                return moved;
            }
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<int, MultiTextureMeshRef> meshrefs = null;
            bool isSealed = itemstack.Attributes.GetBool("isSealed");

            object obj;
            if (capi.ObjectCache.TryGetValue((Variant["metal"]) + "MeshRefs", out obj))
            {
                meshrefs = obj as Dictionary<int, MultiTextureMeshRef>;
            }
            else
            {
                capi.ObjectCache[(Variant["metal"]) + "MeshRefs"] = meshrefs = new Dictionary<int, MultiTextureMeshRef>();
            }

            ItemStack contentStack = GetContent(itemstack);
            if (contentStack == null) return;

            int hashcode = GetSaucepanHashCode(capi.World, contentStack, isSealed);

            MultiTextureMeshRef meshRef = null;

            if (!meshrefs.TryGetValue(hashcode, out meshRef))
            {
                MeshData meshdata = GenRightMesh(capi, contentStack, null, isSealed);
                //meshdata.Rgba2 = null;


                meshrefs[hashcode] = meshRef = capi.Render.UploadMultiTextureMesh(meshdata);

            }
            if (meshRef != null) { renderinfo.ModelRef = meshRef; }

        }

        public string GetOutputText(IWorldAccessor world, InventorySmelting inv)
        {
            List<ItemStack> contents = new List<ItemStack>();
            ItemStack product = null;

            foreach (ItemSlot slot in new ItemSlot[] { inv[3], inv[4], inv[5], inv[6] })
            {
                if (!slot.Empty) contents.Add(slot.Itemstack);
            }

            if (contents.Count == 1)
            {
                product = contents[0].Collectible.CombustibleProps?.SmeltedStack?.ResolvedItemstack;

                if (product == null) return null;

                return Lang.Get("firepit-gui-willcreate", contents[0].StackSize / contents[0].Collectible.CombustibleProps.SmeltedRatio, product.GetName());
            }
            else if (simmerRecipes != null && contents.Count > 1)
            {
                SimmerRecipe match = null;
                int amount = 0;

                foreach (SimmerRecipe rec in simmerRecipes)
                {
                    if (rec.Match(contents) > 0)
                    {
                        amount = rec.Match(contents);
                        match = rec;
                        break;
                    }
                }

                if (match == null) return null;

                product = match.Simmering.SmeltedStack.ResolvedItemstack;

                if (product == null) return null;

                return Lang.Get("firepit-gui-willcreate", amount, product.GetName());
            }

            return null;
        }

        public MeshData GenRightMesh(ICoreClientAPI capi, ItemStack contentStack, BlockPos forBlockPos = null, bool isSealed = false)
        {
            Shape shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/" + (isSealed && Attributes.IsTrue("canSeal") ? "lid" : "empty") + ".json").ToObject<Shape>();
            MeshData bucketmesh;
            capi.Tesselator.TesselateShape(this, shape, out bucketmesh);

            if (contentStack != null)
            {
                WaterTightContainableProps props = GetInContainerProps(contentStack);

                ContainerTextureSource contentSource = new ContainerTextureSource(capi, contentStack, props.Texture);

                MeshData contentMesh;

                if (props.Texture == null) return null;

                float maxLevel = Attributes["maxFillLevel"].AsFloat();
                float fullness = contentStack.StackSize / (props.ItemsPerLitre * CapacityLitres);

                #region Normal Cauldron

                if (maxLevel is 13f)
                {
                    if (fullness <= 0.1f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.1f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.2f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.2f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.3f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.3f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.4f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.4f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.5f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.5f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.6f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.6f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.7f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.7f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.8f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.8f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.9f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.9f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 1f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 1f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                }

                #endregion  Normal Cauldron

                #region Small Cauldron

                if (maxLevel is 8f)
                {
                    if (fullness <= 0.1f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.1f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    if (fullness <= 0.2f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.2f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.3f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.3f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.4f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.4f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.5f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.5f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.6f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.6f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.7f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.7f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.8f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.8f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.9f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.9f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 1f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 1f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                }

                #endregion Small Cauldron

                #region Saucepan

                if (maxLevel is 2f)
                {
                    if (fullness <= 0.1f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.1f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.2f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.2f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.3f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.3f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.4f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.4f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.5f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.5f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.6f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.6f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.7f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.7f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.8f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.8f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.9f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.9f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 1f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + 1f.ToString().Replace(",", ".") + ".json").ToObject<Shape>();
                    }
                }

                #endregion Saucepan


                capi.Tesselator.TesselateShape("saucepan", shape, out contentMesh, contentSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));

                if (props.ClimateColorMap != null)
                {
                    int col = capi.World.ApplyColorMapOnRgba(props.ClimateColorMap, null, ColorUtil.WhiteArgb, 196, 128, false);
                    if (forBlockPos != null)
                    {
                        col = capi.World.ApplyColorMapOnRgba(props.ClimateColorMap, null, ColorUtil.WhiteArgb, forBlockPos.X, forBlockPos.Y, forBlockPos.Z, false);
                    }

                    byte[] rgba = ColorUtil.ToBGRABytes(col);

                    for (int i = 0; i < contentMesh.Rgba.Length; i++)
                    {
                        contentMesh.Rgba[i] = (byte)((contentMesh.Rgba[i] * rgba[i % 4]) / 255);
                    }
                }
                for (int i = 0; i < contentMesh.Flags.Length; i++)
                {
                    contentMesh.Flags[i] = contentMesh.Flags[i] & ~(1 << 12); // Remove water waving flag
                }

                bucketmesh.AddMeshData(contentMesh);
                // Water flags
                
                if (forBlockPos != null)
                {
                    bucketmesh.CustomInts = new CustomMeshDataPartInt(bucketmesh.FlagsCount);
                    bucketmesh.CustomInts.Count = bucketmesh.FlagsCount;
                    bucketmesh.CustomInts.Values.Fill(0x4000000); // light foam only
                    bucketmesh.CustomFloats = new CustomMeshDataPartFloat(bucketmesh.FlagsCount * 2);
                    bucketmesh.CustomFloats.Count = bucketmesh.FlagsCount * 2;
                }
                
            }

            return bucketmesh;
        }

        public int GetSaucepanHashCode(IClientWorldAccessor world, ItemStack contentStack, bool isSealed)
        {
            string s = contentStack.StackSize + "x" + contentStack.Collectible.Code.ToShortString();
            if (isSealed) s += "sealed";
            return s.GetHashCode();
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack drop = base.OnPickBlock(world, pos);

            BlockEntitySaucepan sp = world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySaucepan;

            if (sp != null)
            {
                drop.Attributes.SetBool("isSealed", sp.isSealed);
            }

            return drop;
        }
    }
}
