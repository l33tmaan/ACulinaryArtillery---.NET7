using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using static HarmonyLib.Code;

namespace ACulinaryArtillery
{
    public class ItemExpandedFood : ItemExpandedRawFood
    {
        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            FoodNutritionProperties nutriProps = GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity);
            FoodNutritionProperties[] addProps = GetPropsFromArray((slot.Itemstack.Attributes["expandedSats"] as FloatArrayAttribute)?.value);



            if (byEntity.World is IServerWorldAccessor && nutriProps != null && addProps?.Length > 0 && secondsUsed >= 0.95f)
            {
                TransitionState state = UpdateAndGetTransitionState(api.World, slot, EnumTransitionType.Perish);
                float spoilState = state != null ? state.TransitionLevel : 0;

                float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, byEntity);
                float healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, byEntity);

                foreach (FoodNutritionProperties prop in addProps)
                {
                    //ACulinaryArtillery.logger.Debug("Eated: " + prop.FoodCategory.ToString() + ": " + prop.Satiety.ToString());
                    byEntity.ReceiveSaturation(prop.Satiety * satLossMul, prop.FoodCategory);

                    float healthChange = prop.Health * healthLossMul;

                    if (healthChange != 0)
                    {
                        byEntity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Internal, Type = healthChange > 0 ? EnumDamageType.Heal : EnumDamageType.Poison }, Math.Abs(healthChange));
                    }

                }
            }

            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            ItemStack stack = inSlot.Itemstack;
            EntityPlayer entity = world.Side == EnumAppSide.Client ? (world as IClientWorldAccessor).Player.Entity : null;
            float spoilState = AppendPerishableInfoText(inSlot, new StringBuilder(), world);

            FoodNutritionProperties nutriProps = GetNutritionProperties(world, stack, entity);
            FoodNutritionProperties[] addProps = GetPropsFromArray((inSlot.Itemstack.Attributes["expandedSats"] as FloatArrayAttribute)?.value);

            if (nutriProps != null && addProps?.Length > 0)
            {
                dsc.AppendLine(Lang.Get("efrecipes:Extra Nutrients"));

                foreach (FoodNutritionProperties props in addProps)
                {
                    float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, stack, entity);
                    float healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, stack, entity);
                    //ACulinaryArtillery.logger.Debug(props.FoodCategory.ToString() + ": " + props.Satiety.ToString());
    
                    if (stack.Collectible.MatterState == EnumMatterState.Liquid ) 
                    {
                        float liquidVolume = stack.StackSize; 
                        if (Math.Abs(props.Health * healthLossMul) > 0.001f)
                        {
                            dsc.AppendLine(Lang.Get("efrecipes:- {0} {2} sat, {1} hp", Math.Round((props.Satiety * satLossMul) * (liquidVolume / 10 )), ((props.Health * healthLossMul) * (liquidVolume / 10 )), props.FoodCategory.ToString()));
                        }
                        else
                        {
                            dsc.AppendLine(Lang.Get("efrecipes:- {0} {1} sat", Math.Round((props.Satiety * satLossMul) * (liquidVolume / 10 )), props.FoodCategory.ToString()));
                        }
                    }
                    else 
                    {
                        if (Math.Abs(props.Health * healthLossMul) > 0.001f)
                        {
                            dsc.AppendLine(Lang.Get("efrecipes:- {0} {2} sat, {1} hp", Math.Round(props.Satiety * satLossMul), props.Health * healthLossMul, props.FoodCategory.ToString()));
                        }
                        else
                        {
                            dsc.AppendLine(Lang.Get("efrecipes:- {0} {1} sat", Math.Round(props.Satiety * satLossMul), props.FoodCategory.ToString()));
                        }
                    }
                }
            }


        }       
    }

    
}
