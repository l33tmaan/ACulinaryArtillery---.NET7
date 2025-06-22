using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace ACulinaryArtillery
{
    public class ItemExpandedFood : ItemExpandedRawFood
    {
        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.World is IServerWorldAccessor && GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity) != null &&
                GetPropsFromArray((slot.Itemstack.Attributes["expandedSats"] as FloatArrayAttribute)?.value) is FoodNutritionProperties[] addProps && addProps?.Length > 0 && secondsUsed >= 0.95f)
            {
                float spoilState = UpdateAndGetTransitionState(api.World, slot, EnumTransitionType.Perish)?.TransitionLevel ?? 0;
                float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, byEntity);
                float healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, byEntity);

                foreach (FoodNutritionProperties prop in addProps)
                {
                    byEntity.ReceiveSaturation(prop.Satiety * satLossMul, prop.FoodCategory);

                    float healthChange = prop.Health * healthLossMul;

                    if (healthChange != 0) byEntity.ReceiveDamage(new() { Source = EnumDamageSource.Internal, Type = healthChange > 0 ? EnumDamageType.Heal : EnumDamageType.Poison }, Math.Abs(healthChange));
                }
            }

            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            ItemStack stack = inSlot.Itemstack;
            EntityPlayer? entity = (world as IClientWorldAccessor)?.Player.Entity;
            float spoilState = AppendPerishableInfoText(inSlot, new StringBuilder(), world);

            if (GetNutritionProperties(world, stack, entity) != null && GetPropsFromArray((inSlot.Itemstack.Attributes["expandedSats"] as FloatArrayAttribute)?.value) is FoodNutritionProperties[] addProps && addProps?.Length > 0)
            {
                dsc.AppendLine(Lang.Get("efrecipes:Extra Nutrients"));

                foreach (FoodNutritionProperties props in addProps)
                {
                    float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, stack, entity);
                    float healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, stack, entity);
                    float isLiquidMult = stack.Collectible.MatterState == EnumMatterState.Liquid ? stack.StackSize / 10 : 1;

                    if (Math.Abs(props.Health * healthLossMul) <= 0.001f) dsc.AppendLine(Lang.Get("efrecipes:- {0} {1} sat", Math.Round(props.Satiety * satLossMul * isLiquidMult), props.FoodCategory.ToString()));
                    else dsc.AppendLine(Lang.Get("efrecipes:- {0} {2} sat, {1} hp", Math.Round(props.Satiety * satLossMul * isLiquidMult), props.Health * healthLossMul * isLiquidMult, props.FoodCategory.ToString()));
                }
            }
        }
    }
}