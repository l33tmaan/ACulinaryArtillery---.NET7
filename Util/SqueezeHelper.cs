using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ACulinaryArtillery.Util {
    /// <summary>
    /// <para>
    /// A callback to use to add liquid to a targeted container.
    /// </para>
    /// </summary>
    /// <remarks>Will also mark <see cref="BlockEntityGroundStorage"/> as dirty if applicable and liquid is added.</remarks>
    /// <param name="liquid">Liquid to add.</param>
    /// <param name="amount">Amount to add.</param>
    /// <returns><see langword="true" /> if any amount of liquid is added; <see langword="false"/> otherwise.</returns>
    public delegate bool TryAddLiquidHandler(ItemStack liquid, float amount);

    public static class SqueezeHelper {

        /// <summary>
        /// Checks if <paramref name="slot"/> is suitable to squeeze liquids in there. Includes a check for <see cref="ILiquidInterface.IsFull(ItemStack)"/>.
        /// </summary>
        /// <param name="instance">Instance to check for.</param>
        /// <param name="slot">Slot to check</param>
        public static bool IsSuitableItemSlot(this ItemHoneyComb instance, ItemSlot slot) {
            return slot.Itemstack?.Block is Block slotBlock
                && instance.CanSqueezeInto(slotBlock, null)
                && (slotBlock as ILiquidSink)?.IsFull(slot.Itemstack) == false;
        }

        /// <summary>
        /// Similar implementation as <see cref="ItemHoneyComb.CanSqueezeInto(Block, Vintagestory.API.MathTools.BlockPos)"/> but actually uses 
        /// <paramref name="selection"/> to try to grab a targeted slot before defaulting to the "first available". Also implicitly checks if the target slot 
        /// is already full so we dont pick a "suitable" spot to only later reject it because it has no room.
        /// </summary>
        /// <param name="storage">ground storage</param>
        /// <param name="selection">selection</param>
        /// <param name="instance">honey comb instance</param>
        /// <seealso cref="IsSuitableItemSlot"/>
        public static ItemSlot GetSuitableTargetSlot(this ItemHoneyComb instance, BlockEntityGroundStorage storage, BlockSelection selection) {
            return storage.GetSlotAt(selection) is ItemSlot targetedSlot && instance.IsSuitableItemSlot(targetedSlot)
                ? targetedSlot                                                                                  // pick what player has targeted if suitable
                : storage.Inventory.FirstOrDefault(instance.IsSuitableItemSlot);                                // otherwise pick first available
        }

        /// <summary>
        /// <para>
        /// Utility method to determine if &amp; how to add a liquid to a target and what is potentially already in there.
        /// </para> 
        /// <para>
        /// Eliminates repetitions (and issues) across <see cref="OnHeldInteractStart(ItemSlot, EntityAgent, BlockSelection, EntitySelection, bool, ref EnumHandHandling)"/>
        /// &amp; <see cref="OnHeldInteractStop(float, ItemSlot, EntityAgent, BlockSelection, EntitySelection)"/>
        /// </para>
        /// </summary>
        /// <param name="accessor">Block accessor to use</param>
        /// <param name="selection">Targeted block selection</param>
        /// <param name="block"><em>Optional</em> targeted block. If <see langword="null" /> is passed, will use <paramref name="accessor"/> to get the block for <paramref name="selection"/>. 
        /// If callers have already retrieved the block (or need it for later processing anyway) can be supplied to cut down on block accessor usage.</param>
        /// <param name="blockEntity"><em>Optional</em> targeted block entity. If <see langword="null" /> is passed, will use <paramref name="accessor"/> to get the block entity for <paramref name="selection"/>. 
        /// If callers have already retrieved the block entity (or need it for later processing anyway) can be supplied to cut down on block accessor usage.
        /// </param>
        /// <returns>
        /// <list type="bullet">
        /// <item><see langword="null"/> if there is no suitable container (direct or via ground storage) to add liquids to</item>
        /// <item>otherwise a tuple with members 
        /// <list type="table">
        /// <item>
        /// <term>ExistingStack</term>
        /// <description>The <see cref="ItemStack"/> currently existing inside the targeted liquid container. Will be <see langword="null"/> if the targeted container is empty.</description>
        /// </item>
        /// <item>
        /// <term>TryAddLiquid</term>
        /// <description>A callback to use to try adding liquid to the targeted container. See also <seealso cref="TryAddLiquidHandler"/></description>
        /// </item>
        /// <item>
        /// <term>TargetBlock (preliminary)</term>
        /// <description>The block belonging to the selected target stack. (Intended for animation use).</description>
        /// </item>
        /// </list>
        /// </item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <em>Note:</em> This implementation <em>differs</em> from the default (honeycomb) target selection. Vanilla will always pick the "first available" ground stored 
        /// container. This implementation will pick the <em>targeted</em> container on the ground, and only if that is unavailable it will pick a "first available" fallback.
        /// </remarks>
        public static (ItemStack ExistingStack, TryAddLiquidHandler TryAddLiquid, Block TargetBlock)? GetLiquidOptions(
            this ItemHoneyComb instance,
            IBlockAccessor accessor, 
            BlockSelection selection, 
            Block block = null, 
            BlockEntity blockEntity = null) {

            BlockPos pos = selection.Position;

            return (block ?? accessor.GetBlock(pos), blockEntity ?? accessor.GetBlockEntity(pos), pos) switch {
                // not into sealed saucepans
                (_, BlockEntitySaucepan { isSealed : true}, _) => null,                                                    
                // direct container 
                (ILiquidSink { AllowHeldLiquidTransfer: true } sink, _, var p)
                    when p == null || !sink.IsFull(p) => (
                        ExistingStack: sink.GetContent(p),
                        TryAddLiquid: (liquid, amount) => sink.TryPutLiquid(p, liquid, amount) != 0,
                        TargetBlock: (Block)sink
                    ),
                // no position - no other options
                (_, _, null) => null,
                // we have a position, and are looking at a ground storage
                (_, BlockEntityGroundStorage groundStorage, _) 
                    when instance.GetSuitableTargetSlot(groundStorage, selection) is ItemSlot groundSlot                    // we have a target slot
                        && groundSlot.Itemstack is ItemStack groundStack                                                    // (remember the itemstack) 
                        && groundStack.Block is ILiquidSink { AllowHeldLiquidTransfer: true } sinkInGroundStack             // ensure its a valid liquid sink and remember it
                            => sinkInGroundStack switch { 
                                BlockEntitySaucepan { isSealed: true} => null,                                              // not int osealed saucepans
                                _ => (
                                    ExistingStack: groundStack?.Attributes?.GetTreeAttribute("contents")?.GetItemstack("0"),
                                    TryAddLiquid: (liquid, amount) => {
                                        // note how the put liquid action uses a *different* overload than for the direct injection - see also <see cref="ItemHoneyComb.OnHeldInteractStop" />
                                        bool success = sinkInGroundStack.TryPutLiquid(groundStack, liquid, amount) != 0;
                                        // embed the dirtying into the "add liquid" action
                                        if (success)
                                            groundStorage.MarkDirty(true);
                                        return success;
                                    },
                                    TargetBlock: (Block)sinkInGroundStack
                                ),
                            },                                      
                _ => null
            };
        }
    }
}
