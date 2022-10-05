using ProtoBuf;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RecipeUpload
    {
        public List<string> dvalues;
        public List<string> cvalues;
        public List<string> svalues;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RecipeResponse
    {
        public string response;
    }

    public class RecipeUploadSystem : ModSystem
    {
        #region Client
        IClientNetworkChannel clientChannel;
        ICoreClientAPI clientApi;

        public override void StartClientSide(ICoreClientAPI api)
        {
            clientApi = api;

            clientChannel =
                api.Network.RegisterChannel("networkapitest")
                .RegisterMessageType(typeof(RecipeUpload))
                .RegisterMessageType(typeof(RecipeResponse))
                .SetMessageHandler<RecipeUpload>(OnServerMessage)
            ;
        }

        private void OnServerMessage(RecipeUpload networkMessage)
        {
            List<DoughRecipe> drecipes = new List<DoughRecipe>();
            List<CookingRecipe> crecipes = new List<CookingRecipe>();
            List<SimmerRecipe> srecipes = new List<SimmerRecipe>();

            if (networkMessage.dvalues != null)
            {
                foreach (string drec in networkMessage.dvalues)
                {
                    using (MemoryStream ms = new MemoryStream(Ascii85.Decode(drec)))
                    {
                        BinaryReader reader = new BinaryReader(ms);

                        DoughRecipe retr = new DoughRecipe();
                        retr.FromBytes(reader, clientApi.World);

                        drecipes.Add(retr);
                    }
                }
            }

            MixingRecipeRegistry.Loaded.KneadingRecipes = drecipes;

            if (networkMessage.svalues != null)
            {
                foreach (string srec in networkMessage.svalues)
                {
                    using (MemoryStream ms = new MemoryStream(Ascii85.Decode(srec)))
                    {
                        BinaryReader reader = new BinaryReader(ms);

                        SimmerRecipe retr = new SimmerRecipe();
                        retr.FromBytes(reader, clientApi.World);

                        srecipes.Add(retr);
                    }
                }
            }

            MixingRecipeRegistry.Loaded.SimmerRecipes = srecipes;


            if (networkMessage.cvalues != null)
            {
                foreach (string crec in networkMessage.cvalues)
                {
                    using (MemoryStream ms = new MemoryStream(Ascii85.Decode(crec)))
                    {
                        BinaryReader reader = new BinaryReader(ms);

                        CookingRecipe retr = new CookingRecipe();
                        retr.FromBytes(reader, clientApi.World);

                        crecipes.Add(retr);
                    }
                }
            }
            MixingRecipeRegistry.Loaded.MixingRecipes = crecipes;

            System.Diagnostics.Debug.WriteLine(MixingRecipeRegistry.Loaded.KneadingRecipes.Count + " kneading recipes and " + MixingRecipeRegistry.Loaded.SimmerRecipes.Count + " simmer recipes loaded to client." + MixingRecipeRegistry.Loaded.MixingRecipes.Count + " mixing recipes loaded to client.");
        }

        #endregion

        #region Server
        IServerNetworkChannel serverChannel;
        ICoreServerAPI serverApi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverApi = api;

            serverChannel =
                api.Network.RegisterChannel("networkapitest")
                .RegisterMessageType(typeof(RecipeUpload))
                .RegisterMessageType(typeof(RecipeResponse))
                .SetMessageHandler<RecipeResponse>(OnClientMessage)
            ;

            api.RegisterCommand("recipeupload", "Resync recipes", "", OnRecipeUploadCmd, Privilege.chat);
            api.Event.PlayerNowPlaying += (hmm) => { OnRecipeUploadCmd(); };
        }

        private void OnRecipeUploadCmd(IServerPlayer player = null, int groupId = 0, CmdArgs args = null)
        {
            List<string> drecipes = new List<string>();
            List<string> crecipes = new List<string>();
            List<string> srecipes = new List<string>();

            foreach (DoughRecipe drec in MixingRecipeRegistry.Loaded.KneadingRecipes)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);

                    drec.ToBytes(writer);

                    string value = Ascii85.Encode(ms.ToArray());
                    drecipes.Add(value);
                }
            }

            foreach (CookingRecipe crec in MixingRecipeRegistry.Loaded.MixingRecipes)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);

                    crec.ToBytes(writer);

                    string value = Ascii85.Encode(ms.ToArray());
                    crecipes.Add(value);
                }
            }

            foreach (SimmerRecipe srec in MixingRecipeRegistry.Loaded.SimmerRecipes)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);

                    srec.ToBytes(writer);

                    string value = Ascii85.Encode(ms.ToArray());
                    srecipes.Add(value);
                }
            }

            serverChannel.BroadcastPacket(new RecipeUpload()
            {
                dvalues = drecipes,
                cvalues = crecipes,
                svalues = srecipes
            });
        }

        private void OnClientMessage(IPlayer fromPlayer, RecipeResponse networkMessage)
        {
            OnRecipeUploadCmd();
        }


        #endregion
    }
}
