using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    public class ItemExpandedDough : ItemExpandedRawFood
    {
        ItemStack[]? tableStacks;
        public override void OnLoaded(ICoreAPI api)
        {
            tableStacks ??= [.. api.World.Collectibles.Where(obj => (obj as Block)?.Attributes?["pieFormingSurface"].AsBool() == true).Select(obj => new ItemStack(obj))];

            base.OnLoaded(api);
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            tableStacks = null;

            base.OnUnloaded(api);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel != null)
            {
                var block = api.World.BlockAccessor.GetBlock(blockSel.Position);
                if (block.Attributes?.IsTrue("pieFormingSurface") == true)
                {
                    if (slot.StackSize >= 2) (api.World.GetBlock(new AssetLocation("game:pie-raw")) as BlockPie)?.TryPlacePie(byEntity, blockSel);
                    else (api as ICoreClientAPI)?.TriggerIngameError(this, "notpieable", Lang.Get("Need at least 2 dough"));

                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return [ new() {
                ActionLangCode = "heldhelp-makepie",
                Itemstacks = tableStacks,
                HotKeyCode = "sneak",
                MouseButton = EnumMouseButton.Right,
            }, .. base.GetHeldInteractionHelp(inSlot)];
        }
    }

}