using ACulinaryArtillery.Util;
using HarmonyLib;
using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    public class BlockEntityExpandedOven : BlockEntityOven
    {
        public BlockEntityExpandedOven()
        {
        }
        protected override void OnBurnTick(float dt)
        {
            if (IsBurning) { dt *= ACulinaryArtilleryConfig.Current.BEExpandedOvenFuelBurnRateMod; }
            else { dt *= ACulinaryArtilleryConfig.Current.BEExpandedOvenDTMod; }
            //1.25f is the base vanilla dt modifier for burning ticks, this is a bodge to make our config work without harmony patching the vanilla class.
            base.OnBurnTick(dt/1.25f);
        }
        protected override void IncrementallyBake(float dt, int slotIndex)
        {
            //1.2f is the base vanilla dt modifier for baking speed, this is a bodge to make our config work without harmony patching the vanilla class.
            dt *= ACulinaryArtilleryConfig.Current.BEExpandedOvenBakeTimeMod / 1.2f;
            base.IncrementallyBake(dt, slotIndex);
        }
    }
}
