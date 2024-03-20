using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    public class BlockEntitySpile : BlockEntity
    {
        public double timer;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            RegisterGameTickListener(SapDrip, 5000);
            if (timer == -1000) timer = Api.World.Calendar.TotalHours;
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            timer = Api.World.Calendar.TotalHours;
        }

        public void SapDrip(float dt)
        {
            BlockPos containerpos = posForward(0, -1, 0);
            BlockLiquidContainerBase container = Api.World.BlockAccessor.GetBlock(containerpos) as BlockLiquidContainerBase;
            Block log = Api.World.BlockAccessor.GetBlock(posForward(1, 0, 0));
            if (container == null || log == null) return;
            
            SapProperties xylem = log.Attributes?["sapProperties"]?.AsObject<SapProperties>();
            if (xylem == null) return;

            while (Api.World.Calendar.TotalHours - timer >= xylem.dripTime)
            {
                timer += xylem.dripTime;

                if (Api.World.Rand.NextDouble() > xylem.dripChance || !xylem.seasons.Contains(GetMonth(timer))) return;

                ItemStack drip = new ItemStack(Api.World.GetItem(new AssetLocation(xylem.sap)));

                container.TryPutLiquid(containerpos, drip, xylem.dripCount);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetDouble("timer", timer);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            timer = tree.GetDouble("timer", -1000);
        }

        public int GetMonth(double pastTime)
        {
            double past = (Api.World.Calendar.TotalHours - pastTime)/Api.World.Calendar.HoursPerDay;
            int pastDay = Api.World.Calendar.DayOfYear - (int)past;
            if (pastDay < 0) pastDay += Api.World.Calendar.DaysPerYear;
            
            return (pastDay / Api.World.Calendar.DaysPerMonth) + 1;
        }

        public BlockPos posForward(int offset, int height, int otheraxis)
        {
            switch (Block.Shape.rotateY)
            {
                case 0:
                    return Pos.AddCopy(otheraxis, height, -offset);
                case 180:
                    return Pos.AddCopy(otheraxis, height, offset);
                case 90:
                    return Pos.AddCopy(-offset, height, otheraxis);
                case 270:
                    return Pos.AddCopy(offset, height, otheraxis);
            }

            return Pos;
        }
    }

    public class SapProperties
    {
        public double dripChance = 1;
        public double dripTime = 12;
        public int[] seasons = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        public string sap = "game:waterportion";
        public int dripCount = 1;
    }
}
