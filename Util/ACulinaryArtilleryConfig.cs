using System;
using System.Collections.Generic;
using System.Text;

namespace ACulinaryArtillery.Util
{
    class ACulinaryArtilleryConfig
    {
        public string OvenDTMod = "Modifies The Rate At Which The Oven Heats, Cools, and Bakes";
        public float BEExpandedOvenDTMod;
        public string FuelBurnRateMod = "Multiplier Applied To BurnRate of Fuels Used In Oven";
        public float BEExpandedOvenFuelBurnRateMod;
        public string OvenBakeTimeMod = "Additional Direct Modifier To Bake Rate (Does Not Affect Heating/Cooling Time";
        public float BEExpandedOvenBakeTimeMod;

        public ACulinaryArtilleryConfig()
        { }

        public static ACulinaryArtilleryConfig Current { get; set; }

        public static ACulinaryArtilleryConfig GetDefault()
        {
            ACulinaryArtilleryConfig defaultConfig = new();

            defaultConfig.OvenDTMod.ToString();
            defaultConfig.BEExpandedOvenDTMod = 0.025f;
            defaultConfig.FuelBurnRateMod.ToString();
            defaultConfig.BEExpandedOvenFuelBurnRateMod = 1f;
            defaultConfig.OvenBakeTimeMod.ToString();
            defaultConfig.BEExpandedOvenBakeTimeMod = 1.2f;
            return defaultConfig;
        }
    }
}