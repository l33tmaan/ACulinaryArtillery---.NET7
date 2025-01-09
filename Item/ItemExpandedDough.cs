using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    public class ItemExpandedDough : ItemExpandedRawFood
    {
        ItemStack[] tableStacks;
        public override void OnLoaded(ICoreAPI api)
        {
            if (tableStacks == null)
            {
                List<ItemStack> stacks = new List<ItemStack>();
                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj is Block block && block.Attributes?.IsTrue("pieFormingSurface") == true)
                    {
                        stacks.Add(new ItemStack(obj));
                    }
                }

                tableStacks = stacks.ToArray();
            }
            base.OnLoaded(api);
;
        }
        public override void OnUnloaded(ICoreAPI api)
        {
            tableStacks = null;
            base.OnUnloaded(api);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel != null)
            {
                var block = api.World.BlockAccessor.GetBlock(blockSel.Position);
                if (block.Attributes?.IsTrue("pieFormingSurface") == true)
                {
                    if (slot.StackSize >= 2)
                    {
                        BlockPie blockform = api.World.GetBlock(new AssetLocation("game:pie-raw")) as BlockPie;
                        blockform.TryPlacePie(byEntity, blockSel);
                    }
                    else
                    {
                        ICoreClientAPI capi = api as ICoreClientAPI;
                        if (capi != null) capi.TriggerIngameError(this, "notpieable", Lang.Get("Need at least 2 dough"));
                    }
                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
            }
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-makepie",
                    Itemstacks = tableStacks,
                    HotKeyCode = "sneak",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }

    
}
