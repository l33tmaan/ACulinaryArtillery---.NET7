using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ACulinaryArtillery
{
    public class ItemTransFix : Item
    {
        public override TransitionState[] UpdateAndGetTransitionStates(IWorldAccessor world, ItemSlot inslot)
        {
            ItemStack itemstack = inslot?.Itemstack;

            TransitionableProperties[] propsm = GetTransitionableProperties(world, inslot?.Itemstack, null);

            if (itemstack == null || propsm == null || propsm.Length == 0)
            {
                return null;
            }

            if (itemstack.Attributes == null)
            {
                itemstack.Attributes = new TreeAttribute();
            }

            if (!(itemstack.Attributes["transitionstate"] is ITreeAttribute))
            {
                itemstack.Attributes["transitionstate"] = new TreeAttribute();
            }

            ITreeAttribute attr = (ITreeAttribute)itemstack.Attributes["transitionstate"];

            //TransitionableProperties[] props = itemstack.Collectible.TransitionableProps; - WTF is this here for? we already have propsm

            float[] transitionedHours;
            float[] freshHours;
            float[] transitionHours;
            TransitionState[] states = new TransitionState[propsm.Length];

            if (!attr.HasAttribute("createdTotalHours"))
            {
                attr.SetDouble("createdTotalHours", world.Calendar.TotalHours);
                attr.SetDouble("lastUpdatedTotalHours", world.Calendar.TotalHours);

                freshHours = new float[propsm.Length];
                transitionHours = new float[propsm.Length];
                transitionedHours = new float[propsm.Length];

                for (int i = 0; i < propsm.Length; i++)
                {
                    transitionedHours[i] = 0;
                    if (propsm[i] != null)
                    {
                        freshHours[i] = propsm[i].FreshHours.nextFloat(1, world.Rand);
                        transitionHours[i] = propsm[i].TransitionHours.nextFloat(1, world.Rand);
                    } else
                    {
                        freshHours[i] = 0;
                        transitionHours[i] = 0;
                    }
                }

                attr["freshHours"] = new FloatArrayAttribute(freshHours);
                attr["transitionHours"] = new FloatArrayAttribute(transitionHours);
                attr["transitionedHours"] = new FloatArrayAttribute(transitionedHours);
            }
            else
            {
                freshHours = (attr["freshHours"] as FloatArrayAttribute).value;
                transitionHours = (attr["transitionHours"] as FloatArrayAttribute).value;
                transitionedHours = (attr["transitionedHours"] as FloatArrayAttribute).value;
            }

            double lastUpdatedTotalHours = attr.GetDouble("lastUpdatedTotalHours");
            double nowTotalHours = world.Calendar.TotalHours;


            bool nowSpoiling = false;

            float hoursPassed = (float)(nowTotalHours - lastUpdatedTotalHours);

            for (int i = 0; i < propsm.Length; i++)
            {
                TransitionableProperties prop = propsm[i];
                if (prop == null || i >= freshHours.Length) continue;

                float transitionRateMul = GetTransitionRateMul(world, inslot, prop.Type);

                if (hoursPassed > 0.05f) // Maybe prevents us from running into accumulating rounding errors?
                {
                    float hoursPassedAdjusted = hoursPassed * transitionRateMul;
                    transitionedHours[i] += hoursPassedAdjusted;

                    /*if (api.World.Side == EnumAppSide.Server && inslot.Inventory.ClassName == "chest")
                    {
                        Console.WriteLine(hoursPassed + " hours passed. " + inslot.Itemstack.Collectible.Code + " spoil by " + transitionRateMul + "x. Is inside " + inslot.Inventory.ClassName + " {0}/{1}", transitionedHours[i], freshHours[i]);
                    }*/
                }

                float freshHoursLeft = Math.Max(0, freshHours[i] - transitionedHours[i]);
                float transitionLevel = Math.Max(0, transitionedHours[i] - freshHours[i]) / transitionHours[i];

                // Don't continue transitioning spoiled foods
                if (transitionLevel > 0)
                {
                    if (prop.Type == EnumTransitionType.Perish)
                    {
                        nowSpoiling = true;
                    }
                    else
                    {
                        if (nowSpoiling) continue;
                    }
                }

                if (transitionLevel >= 1 && world.Side == EnumAppSide.Server)
                {
                    ItemStack newstack = OnTransitionNow(inslot, itemstack.Collectible.TransitionableProps[i]);

                    if (newstack.StackSize <= 0)
                    {
                        inslot.Itemstack = null;
                    }
                    else
                    {
                        itemstack.SetFrom(newstack);
                    }

                    inslot.MarkDirty();

                    // Only do one transformation, then do the next one next update
                    // This does fully not respect time-fast-forward, so that should be fixed some day
                    break;
                }

                states[i] = new TransitionState()
                {
                    FreshHoursLeft = freshHoursLeft,
                    TransitionLevel = Math.Min(1, transitionLevel),
                    TransitionedHours = transitionedHours[i],
                    TransitionHours = transitionHours[i],
                    FreshHours = freshHours[i],
                    Props = prop
                };

                //if (transitionRateMul > 0) break; // Only do one transformation at the time (i.e. food can not cure and perish at the same time) - Tyron 9/oct 2020, but why not at the same time? We need it for cheese ripening
            }

            if (hoursPassed > 0.05f)
            {
                attr.SetDouble("lastUpdatedTotalHours", nowTotalHours);
            }

            return states.Where(s => s != null).OrderBy(s => (int)s.Props.Type).ToArray();
        }

        public override TransitionState UpdateAndGetTransitionState(IWorldAccessor world, ItemSlot inslot, EnumTransitionType type)
        {
            TransitionState[] states = UpdateAndGetTransitionStates(world, inslot);
            TransitionableProperties[] propsm = GetTransitionableProperties(world, inslot?.Itemstack, null);
            if (propsm == null) return null;

            for (int i = 0; i < propsm.Length; i++)
            {
                if (i >= states.Length) break;
                if (propsm[i]?.Type == type) return states[i];
            }

            return null;
        }
    }
}
