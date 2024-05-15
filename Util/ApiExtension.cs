using ACulinaryArtillery;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent;

public static class ApiAdditions
{
    public static List<CookingRecipe> GetMixingRecipes(this ICoreAPI api)
    {
        return api.ModLoader.GetModSystem<ACARecipeRegistrySystem>().MixingRecipes;
    }
    public static List<SimmerRecipe> GetSimmerRecipes(this ICoreAPI api)
    {
        return api.ModLoader.GetModSystem<ACARecipeRegistrySystem>().SimmerRecipes;
    }
    public static List<DoughRecipe> GetKneadingRecipes(this ICoreAPI api)
    {
        return api.ModLoader.GetModSystem<ACARecipeRegistrySystem>().DoughRecipes;
    }
}