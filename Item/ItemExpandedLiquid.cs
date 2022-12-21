using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;



namespace ACulinaryArtillery
{
    public class ItemExpandedLiquid : ItemExpandedFood
    {
        public override void OnGroundIdle(EntityItem entityItem)
        {
            entityItem.Die(EnumDespawnReason.Removed);

            if (entityItem.World.Side == EnumAppSide.Server)
            {
                entityItem.World.SpawnCubeParticles(entityItem.SidedPos.XYZ, entityItem.Itemstack, 0.75f, 25 * entityItem.Itemstack.StackSize, 0.45f);
                entityItem.World.PlaySoundAt(new AssetLocation("sounds/environment/smallsplash"), (float)entityItem.SidedPos.X, (float)entityItem.SidedPos.Y, (float)entityItem.SidedPos.Z, null);
            }


            base.OnGroundIdle(entityItem);

        }
/*        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
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
                    float liquid = stack.StackSize; 
                    float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, stack, entity);
                    float healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, stack, entity);

                    if (Math.Abs(props.Health * healthLossMul) > 0.001f)
                    {
                        dsc.AppendLine(Lang.Get("efrecipes:- {0} {2} sat, {1} hp", Math.Round((props.Satiety * satLossMul) * (liquid / 10 )), ((props.Health * healthLossMul) * (liquid / 10 )), props.FoodCategory.ToString()));
                    }
                    else
                    {
                        dsc.AppendLine(Lang.Get("efrecipes:- {0} {1} sat", Math.Round((props.Satiety * satLossMul) * (liquid / 10 )), props.FoodCategory.ToString()));
                    }
                }
            }


        }   */
    }
}
