using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace ACulinaryArtillery.Util {
    public static class SqueezeHelper {

        public static bool IsSuitableItemSlot(this ItemHoneyComb instance, ItemSlot slot) {
            return slot.Itemstack?.Block is Block slotBlock && instance.CanSqueezeInto(slotBlock, null);
        }

        /// <summary>
        /// Similar implementation as <see cref="ItemHoneyComb.CanSqueezeInto(Block, Vintagestory.API.MathTools.BlockPos)"/> but actually uses 
        /// <paramref name="selection"/> to try to grab a targeted slot before defaulting to the "first available".
        /// </summary>
        /// <param name="storage">ground storage</param>
        /// <param name="selection">selection</param>
        /// <param name="instance">honey comb instance</param>
        public static ItemSlot GetSuitableTargetSlot(this ItemHoneyComb instance, BlockEntityGroundStorage storage, BlockSelection selection) {
            return storage.GetSlotAt(selection) is ItemSlot targetedSlot && instance.IsSuitableItemSlot(targetedSlot)
                ? targetedSlot                                                                                  // pick what player has targeted if suitable
                : storage.Inventory.FirstOrDefault(instance.IsSuitableItemSlot);                                // otherwise pick first available
        }
    }
}
