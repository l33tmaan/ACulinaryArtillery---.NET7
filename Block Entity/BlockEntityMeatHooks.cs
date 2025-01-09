using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using System.Diagnostics;

namespace ACulinaryArtillery
{
    public class BlockEntityMeatHooks : BlockEntityDisplayCase, ITexPositionSource
    {
        public override string InventoryClassName => "meathooks";
        //protected InventoryGeneric inventory;
        public override string AttributeTransformCode => "meatHookTransform";
        public override InventoryBase Inventory => inventory;

        public BlockEntityMeatHooks()
        {
            inventory = new InventoryDisplayed(this, 4, "meathooks-0", null, null);
            // meshes = new MeshData[4];
            var meshes = new MeshData[4];
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            RegisterGameTickListener(RotDrop, 3000);
            Inventory.OnAcquireTransitionSpeed += Inventory_OnAcquireTransitionSpeed;
        }

        private void RotDrop(float dt)
        {
            for (int i = 0; i < 4; i++)
            {
                if (inventory[i].Itemstack?.Collectible.LastCodePart() == "rot") inventory.DropSlots(Pos.ToVec3d().Add(0.5, -1, 0.5), i);
            }
        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            //System.Diagnostics.Debug.WriteLine(blockSel.SelectionBoxIndex);
            if (slot.Empty)
            {
                if (TryTake(byPlayer, blockSel))
                {
                    return true;
                }
                return false;
            }
            else
            {
                CollectibleObject colObj = slot.Itemstack.Collectible;
                if (colObj.Attributes != null && colObj.Attributes["meathookable"].AsBool(false) == true)
                {
                    AssetLocation sound = slot.Itemstack?.Block?.Sounds?.Place;

                    if (TryPut(slot, blockSel))
                    {
                        Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                        return true;
                    }

                    return false;
                }
            }


            return false;
        }

        private float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
        {
            if (Api == null) return 1;
            if (transType == EnumTransitionType.Cure) return Block.Attributes["cureRate"].AsFloat(3);
            if (transType == EnumTransitionType.Dry) return Block.Attributes["dryRate"].AsFloat(3);
            return baseMul;

        }

        private bool TryPut(ItemSlot slot, BlockSelection blockSel)
        {
            int index = blockSel.SelectionBoxIndex;

            if (inventory[index].Empty)
            {
                int moved = slot.TryPutInto(Api.World, inventory[index]);

                if (moved > 0)
                {
                    updateMesh(index);

                    MarkDirty(true);
                }

                return moved > 0;
            }

            return false;
        }

        private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
        {
            int index = blockSel.SelectionBoxIndex;

            if (!inventory[index].Empty)
            {
                ItemStack stack = inventory[index].TakeOut(1);
                if (byPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    AssetLocation sound = stack.Block?.Sounds?.Place;
                    Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                }

                if (stack.StackSize > 0)
                {
                    Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }

                updateMesh(index);
                MarkDirty(true);
                return true;
            }

            return false;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            //base.GetBlockInfo(forPlayer, sb);

            sb.AppendLine();

            if (forPlayer?.CurrentBlockSelection == null) return;

            int index = forPlayer.CurrentBlockSelection.SelectionBoxIndex;

            if (!inventory[index].Empty)
            {
                if (inventory[index].Itemstack.Collectible.TransitionableProps != null && inventory[index].Itemstack.Collectible.TransitionableProps.Length > 0)
                {
                    sb.AppendLine(PerishableInfoCompact(Api, inventory[index], 0));
                }
                else sb.AppendLine(inventory[index].Itemstack.GetName());
            }
        }

        protected override float[][] genTransformationMatrices()
        {
            float[][] tfMatrices = new float[4][];
            //Api.Logger.Debug(Block.ToString());
            //Api.Logger.Debug(Block.Shape.rotateY.ToString());
            for (int index = 0; index < 4; index++)
            {
                var selectionBox = this.Block.SelectionBoxes[index];
                
                float x;
                float y;
                float z;

                //x = (index % 2 == 0) ? 5 / 16f : 11 / 16f;
                //y = 2 / 16f;
                //z = (index > 1) ? 11 / 16f : 5 / 16f;

                x = selectionBox.MidX;
                y = selectionBox.MaxY;
                z = selectionBox.MidZ;

                int rnd = GameMath.MurmurHash3Mod(Pos.X, Pos.Y + index * 50, Pos.Z, 30) - 15;
                var collObjAttr = inventory[index]?.Itemstack?.Collectible?.Attributes;
                //Api.Logger.Debug(rnd.ToString());
                if (collObjAttr != null && collObjAttr["randomizeInDisplayCase"].AsBool(true) == false)
                {
                    rnd = 0;
                }

                //float degY = (90 + rnd);
                //Api.Logger.Debug(String.Format("Item:{0} | Index: {1}", inventory[index]?.Itemstack?.GetName(), index));
                tfMatrices[index] = 
                    new Matrixf()
                    .Translate(0.5f, 0, 0.5f)
                    .Translate(x - 0.5f, y, z - 0.5f)
                    .RotateYDeg(getRotateOnHook(index)+rnd)
                    .Scale(0.75f, 0.75f, 0.75f)
                    .Translate(-0.5f, 0, -0.5f)
                    .Values
                ;
            }

            return tfMatrices;
        }
        private float getRotateOnHook(int index)
        {
            return Block.Shape.rotateY switch
            {
                90 => index % 2 == 0 ? Block.Shape.rotateY + 180 : Block.Shape.rotateY,
                180 => index < 2 ? Block.Shape.rotateY : Block.Shape.rotateY + 180,
                270 => index % 2 == 0 ? Block.Shape.rotateY : Block.Shape.rotateY + 180,
                _ => index < 2 ? Block.Shape.rotateY + 180 : Block.Shape.rotateY,
            };
        }
        public string PerishableInfoCompact(ICoreAPI Api, ItemSlot contentSlot, float ripenRate, bool withStackName = true)
        {
            if (contentSlot.Empty) return "";

            StringBuilder dsc = new StringBuilder();

            if (withStackName)
            {
                dsc.Append(contentSlot.Itemstack.GetName());
            }

            TransitionState[] transitionStates = contentSlot.Itemstack?.Collectible.UpdateAndGetTransitionStates(Api.World, contentSlot);

            bool nowSpoiling = false;

            if (transitionStates != null)
            {
                bool appendLine = false;
                for (int i = 0; i < transitionStates.Length; i++)
                {
                    TransitionState state = transitionStates[i];

                    TransitionableProperties prop = state.Props;
                    float perishRate = contentSlot.Itemstack.Collectible.GetTransitionRateMul(Api.World, contentSlot, prop.Type);

                    if (perishRate <= 0) continue;

                    float transitionLevel = state.TransitionLevel;
                    float freshHoursLeft = state.FreshHoursLeft / perishRate;
                    double hoursPerday = Api.World.Calendar.HoursPerDay;
                    switch (prop.Type)
                    {
                        case EnumTransitionType.Perish:

                            appendLine = true;

                            if (transitionLevel > 0)
                            {
                                nowSpoiling = true;
                                dsc.Append("\n" + Lang.Get("itemstack-perishable-spoiling", (int)Math.Round(transitionLevel * 100)));
                            }
                            else
                            {

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append("\n" + Lang.Get("itemstack-perishable-fresh-years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append("\n" + Lang.Get("itemstack-perishable-fresh-days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append("\n" + Lang.Get("itemstack-perishable-fresh-hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;

                        case EnumTransitionType.Cure:
                            if (nowSpoiling) break;

                            appendLine = true;

                            if (transitionLevel > 0)
                            {
                                int hoursLeft = (int)((state.TransitionHours - (state.TransitionedHours - state.FreshHours)) / Block.Attributes["cureRate"].AsFloat(3f));
                                dsc.Append("\n" + Lang.Get("itemstack-curable-cured", Math.Round(transitionLevel * 100)));
                                if (hoursLeft > hoursPerday)
                                {
                                    dsc.Append(", " + Lang.Get("{0:0.#} days left", hoursLeft / hoursPerday));
                                }
                                else
                                {
                                    dsc.Append(", " + Lang.Get("{0:0} hrs left", hoursLeft));
                                }
                                
                                //dsc.Append(", " + Lang.Get("{1:0.#} days left to cure ({0}%)", (int)Math.Round(transitionLevel * 100), (state.TransitionHours - (state.TransitionedHours - state.FreshHours)) / Api.World.Calendar.HoursPerDay / Block.Attributes["cureRate"].AsFloat(3f)));
                            }
                            else
                            {


                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append("\n" + Lang.Get("will cure in ") + Lang.Get("{0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    //dsc.Append(", " + Lang.Get("will dry in ") + Lang.Get("{0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                    dsc.Append("\n" + Lang.Get("itemstack-curable-duration-days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    //dsc.Append(", " + Lang.Get("will dry in ") + Lang.Get("{0} hours", Math.Round(freshHoursLeft, 1)));
                                    dsc.Append("\n" + Lang.Get("itemstack-curable-duration-hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;
                        case EnumTransitionType.Dry:
                            if (nowSpoiling) break;

                            appendLine = true;

                            if (transitionLevel > 0)
                            {
                                int hoursLeft = (int)((state.TransitionHours - (state.TransitionedHours - state.FreshHours)) / Block.Attributes["dryRate"].AsFloat(6f));
                                dsc.Append("\n" + Lang.Get("itemstack-dryable-dried", Math.Round(transitionLevel * 100)));
                                if (hoursLeft > hoursPerday) 
                                {
                                    dsc.Append(", " + Lang.Get("{0:0.#} days left", hoursLeft / hoursPerday));
                                }
                                else
                                {
                                    dsc.Append(", " + Lang.Get("{0:0} hrs left", hoursLeft));
                                }
                                
                                //dsc.Append(", " + Lang.Get("{1:0.#} days left to dry ({0}%)", (int)Math.Round(transitionLevel * 100), (state.TransitionHours - (state.TransitionedHours - state.FreshHours)) / Api.World.Calendar.HoursPerDay / Block.Attributes["dryRate"].AsFloat(6f)));
                            }
                            else
                            {
                                

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append("\n" + Lang.Get("will dry in ") + Lang.Get("{0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                   // dsc.Append(", " + Lang.Get("will dry in ") + Lang.Get("{0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                    dsc.Append("\n" + Lang.Get("itemstack-dryable-duration-days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    //dsc.Append(", " + Lang.Get("will dry in ") + Lang.Get("{0} hours", Math.Round(freshHoursLeft, 1)));
                                    dsc.Append("\n" + Lang.Get("itemstack-dryable-duration-hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;
                    }
                }


                if (appendLine) dsc.AppendLine();
            }

            return dsc.ToString();
        }

    }

}
