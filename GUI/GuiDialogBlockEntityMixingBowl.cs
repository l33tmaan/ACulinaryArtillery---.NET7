using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
//using System.Diagnostics;

namespace ACulinaryArtillery
{
    public class GuiDialogBlockEntityMixingBowl : GuiDialogBlockEntity
    {
        long lastRedrawMs;
        string globalOutputText;

        protected override double FloatyDialogPosition => 0.75;

        public GuiDialogBlockEntityMixingBowl(string DialogTitle, InventoryBase Inventory, BlockPos BlockEntityPosition, ICoreClientAPI capi)
            : base(DialogTitle, Inventory, BlockEntityPosition, capi)
        {
            if (IsDuplicate) return;

            capi.World.Player.InventoryManager.OpenInventory(Inventory);

            SetupDialog();
        }

        private void OnInventorySlotModified(int slotid)
        {
            // Direct call can cause InvalidOperationException
            capi.Event.EnqueueMainThreadTask(SetupDialog, "setupmixingbowldlg");
        }

        void SetupDialog()
        {
            ItemSlot hoveredSlot = capi.World.Player.InventoryManager.CurrentHoveredSlot;
            if (hoveredSlot != null && hoveredSlot.Inventory == Inventory)
            {
                capi.Input.TriggerOnMouseLeaveSlot(hoveredSlot);
            }
            else
            {
                hoveredSlot = null;
            }

            

            ElementBounds ingredSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 30+45, 6, 1);
            ingredSlotBounds.fixedHeight += 10;
            double top = ingredSlotBounds.fixedHeight + ingredSlotBounds.fixedY;

            ElementBounds quernBounds = ElementBounds.Fixed(0, top - 30, 200, 90);

            ElementBounds inputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, top, 1, 1);
            
            ElementBounds outputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 153, top, 1, 1);

            // 2. Around all that is 10 pixel padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(quernBounds);

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

            ClearComposers();

            // I may or may not have added this shit
            string ot = "";
            if (globalOutputText != null)
            { ot = globalOutputText; }

            SingleComposer = capi.Gui
                .CreateCompo("blockentitymixingbowl" + BlockEntityPosition, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddDynamicCustomDraw(quernBounds, OnBgDraw, "symbolDrawer")
                    .AddDynamicText(ot, CairoFont.WhiteDetailText().WithOrientation(EnumTextOrientation.Left), ElementBounds.Fixed(0, 30, 210, 45), "outputText")
                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new int[] { 0 }, inputSlotBounds, "inputSlot")
                    .AddItemSlotGrid(Inventory, SendInvPacket, 6, new int[] { 2, 3, 4, 5, 6, 7 }, ingredSlotBounds, "ingredSlots")
                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new int[] { 1 }, outputSlotBounds, "outputslot")
                .EndChildElements()
                .Compose()
            ;

            lastRedrawMs = capi.ElapsedMilliseconds;

            if (hoveredSlot != null)
            {
                SingleComposer.OnMouseMove(new MouseEvent(capi.Input.MouseX, capi.Input.MouseY));
            }
        }

        float inputGrindTime;
        float maxGrindTime;
        public void Update(float inputGrindTime, float maxGrindTime, string outputText)
        {
            this.inputGrindTime = inputGrindTime;
            this.maxGrindTime = maxGrindTime;

            if (!IsOpened()) return;
            SingleComposer.GetDynamicText("outputText").SetNewText(outputText);

            if (capi.ElapsedMilliseconds - lastRedrawMs > 500)
            {
                if (SingleComposer != null) SingleComposer.GetCustomDraw("symbolDrawer").Redraw();
                lastRedrawMs = capi.ElapsedMilliseconds;
            }
            // don't ask, don't tell
            globalOutputText = outputText;
        }



        private void OnBgDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            double top = 30;

            // Arrow Right
            ctx.Save();
            Matrix m = ctx.Matrix;
            m.Translate(GuiElement.scaled(63), GuiElement.scaled(top + 2));
            m.Scale(GuiElement.scaled(0.6), GuiElement.scaled(0.6));
            ctx.Matrix = m;
            capi.Gui.Icons.DrawArrowRight(ctx, 2);

            double dx = inputGrindTime / maxGrindTime;


            ctx.Rectangle(GuiElement.scaled(5), 0, GuiElement.scaled(125 * dx), GuiElement.scaled(100));
            ctx.Clip();
            LinearGradient gradient = new LinearGradient(0, 0, GuiElement.scaled(200), 0);
            gradient.AddColorStop(0, new Color(0, 0.4, 0, 1));
            gradient.AddColorStop(1, new Color(0.2, 0.6, 0.2, 1));
            ctx.SetSource(gradient);
            capi.Gui.Icons.DrawArrowRight(ctx, 0, false, false);
            gradient.Dispose();
            ctx.Restore();
        }




        private void SendInvPacket(object p)
        {
            capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, p);
        }


        private void OnTitleBarClose()
        {
            TryClose();
        }


        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Inventory.SlotModified += OnInventorySlotModified;
        }

        public override void OnGuiClosed()
        {
            Inventory.SlotModified -= OnInventorySlotModified;

            SingleComposer.GetSlotGrid("inputSlot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("outputslot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("ingredSlots").OnGuiClosed(capi);

            base.OnGuiClosed();
        }
    }
}
