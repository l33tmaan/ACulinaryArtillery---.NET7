using Vintagestory.API.Common;

namespace ACulinaryArtillery
{
    public class ItemTransLiquid : ItemTransFix
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
    }
}
