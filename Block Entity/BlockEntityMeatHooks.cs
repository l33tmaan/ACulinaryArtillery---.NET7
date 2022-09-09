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
    public class BlockEntityMeatHooks : BlockEntityDisplay
    {
        public override string InventoryClassName => "meathooks";
        protected InventoryGeneric inventory;
        public override string AttributeTransformCode => "meatHookTransform";
        public override InventoryBase Inventory => inventory;

        public BlockEntityMeatHooks()
        {
            inventory = new InventoryDisplayed(this, 4, "meathooks-0", null, null);
            meshes = new MeshData[4];
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            RegisterGameTickListener(RotDrop, 3000);
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

        protected override float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
        {
            if (Api == null) return 1;

            if (transType == EnumTransitionType.Cure) return Block.Attributes["cureRate"].AsFloat(3);
            if (transType == EnumTransitionType.Dry) return Block.Attributes["dryRate"].AsFloat(3);


            return base.Inventory_OnAcquireTransitionSpeed(transType, stack, baseMul);

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
            base.GetBlockInfo(forPlayer, sb);

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

        public override void TranslateMesh(MeshData mesh, int index)
        {
            float x = (index % 2 == 0) ? 5 / 16f : 11 / 16f;
            float y = 1 / 16f;
            float z = (index > 1) ? 11 / 16f : 5 / 16f;
            float rotY = 0;
            if (Block.Shape.rotateY == 0)
            {
                if (index == 2 || index == 3)
                {
                    rotY = 180 * GameMath.DEG2RAD;
				}
			}
            else if (Block.Shape.rotateY == 90)
            {
                if (index == 0 || index == 2)
                {
                    rotY = 0 * GameMath.DEG2RAD;
                }
                else
                {
                    rotY = 180 * GameMath.DEG2RAD;
                }
            }
            else if (Block.Shape.rotateY == 180)
            {
                if (index == 0 || index == 1)
                {
                    rotY = 180 * GameMath.DEG2RAD;
				}
			}
            else if (Block.Shape.rotateY == 270 )
            {
                if (index == 0 || index == 2)
                {
                    rotY = 180 * GameMath.DEG2RAD;
                }
                else
                {
                    rotY = 0 * GameMath.DEG2RAD;
                }
            }
            mesh.Rotate(new Vec3f(0.5f, 0, 0.5f), 0, rotY, 0);
            mesh.Translate(x - 0.5f, 0, z - 0.5f);
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

                    switch (prop.Type)
                    {
                        case EnumTransitionType.Perish:

                            appendLine = true;

                            if (transitionLevel > 0)
                            {
                                nowSpoiling = true;
                                dsc.Append(", " + Lang.Get("{0}% spoiled", (int)Math.Round(transitionLevel * 100)));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(", " + Lang.Get("fresh for {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(", " + Lang.Get("fresh for {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(", " + Lang.Get("fresh for {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;

                        case EnumTransitionType.Cure:
                            if (nowSpoiling) break;

                            appendLine = true;

                            if (transitionLevel > 0)
                            {
                                dsc.Append(", " + Lang.Get("{1:0.#} days left to cure ({0}%)", (int)Math.Round(transitionLevel * 100), (state.TransitionHours - state.TransitionedHours) / Api.World.Calendar.HoursPerDay / Block.Attributes["cureRate"].AsFloat(3f)));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(", " + Lang.Get("will cure in {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(", " + Lang.Get("will cure in {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(", " + Lang.Get("will cure in {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;
                        case EnumTransitionType.Dry:
                            if (nowSpoiling) break;

                            appendLine = true;

                            if (transitionLevel > 0)
                            {
                                dsc.Append(", " + Lang.Get("{1:0.#} days left to dry ({0}%)", (int)Math.Round(transitionLevel * 100), (state.TransitionHours - state.TransitionedHours) / Api.World.Calendar.HoursPerDay / Block.Attributes["dryRate"].AsFloat(6f)));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(", " + Lang.Get("will dry in {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(", " + Lang.Get("will dry in {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(", " + Lang.Get("will dry in {0} hours", Math.Round(freshHoursLeft, 1)));
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
