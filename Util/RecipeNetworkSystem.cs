using ProtoBuf;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
/*
namespace ACulinaryArtillery
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RecipeUpload
    {
        public List<string> dvalues;
        public List<string> cvalues;
        public List<string> svalues;
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
                api.Network.RegisterChannel("aculinaryartillery")
                .RegisterMessageType(typeof(RecipeUpload))
                .SetMessageHandler<RecipeUpload>(OnServerMessage)
            ;
        }

        private void OnServerMessage(RecipeUpload networkMessage)
        {
            if (clientApi.IsSinglePlayer || !clientApi.PlayerReadyFired ) return;
            clientApi.Logger.VerboseDebug("Received ACA recipes from server");
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

            MixingRecipeRegistry.Registry.KneadingRecipes = drecipes;

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

            MixingRecipeRegistry.Registry.SimmerRecipes = srecipes;

            /*
            if (networkMessage.cvalues != null)
            {
                foreach (string crec in networkMessage.cvalues)
                {
                    using (MemoryStream ms = new MemoryStream(Ascii85.Decode(crec)))
                    {
                        BinaryReader reader = new BinaryReader(ms);

                        CookingRecipe retr = new CookingRecipe();
                        retr.FromBytes(reader, clientApi.World);
                        if (!CookingRecipe.NamingRegistry.ContainsKey(retr.Code))
                        {
                            CookingRecipe.NamingRegistry[retr.Code] = new acaRecipeNames();
                        }
                        crecipes.Add(retr);
                    }
                }
            }
            MixingRecipeRegistry.Registry.MixingRecipes = crecipes;
            */
/*
            System.Diagnostics.Debug.WriteLine(MixingRecipeRegistry.Registry.KneadingRecipes.Count + " kneading recipes and " + MixingRecipeRegistry.Registry.SimmerRecipes.Count + " simmer recipes loaded to client." + MixingRecipeRegistry.Registry.MixingRecipes.Count + " mixing recipes loaded to client.");
        }

        #endregion

        #region Server
        IServerNetworkChannel serverChannel;
        ICoreServerAPI serverApi;
        RecipeUpload cachedMessage;

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverApi = api;

            serverChannel =
                api.Network.RegisterChannel("aculinaryartillery")
                .RegisterMessageType(typeof(RecipeUpload))
            ;

            api.RegisterCommand("recipeupload", "Resync recipes", "", OnRecipeUploadCmd, Privilege.controlserver);
            //api.Event.PlayerNowPlaying += (player) => { SendRecepies(player); };
        }

        private void OnRecipeUploadCmd(IServerPlayer player = null, int groupId = 0, CmdArgs args = null)
        {
            BroadcastRecepies();
        }

        private void BroadcastRecepies()
        {
            RecipeUpload message = GetRecipeUploadMessage();
            cachedMessage = message;

            serverChannel.BroadcastPacket(message);
        }

        private void SendRecepies(IServerPlayer player, bool allowCache = true)
        {
                SendRecepies(new IServerPlayer[] { player }, allowCache);
            
        }

        private void SendRecepies(IServerPlayer[] players, bool allowCache = true)
        {
            if (!allowCache || cachedMessage == null)
                cachedMessage = GetRecipeUploadMessage();

            serverChannel.SendPacket(cachedMessage, players);
        }

        private RecipeUpload GetRecipeUploadMessage()
        {
            List<string> drecipes = new List<string>();
            List<string> crecipes = new List<string>();
            List<string> srecipes = new List<string>();

            foreach (DoughRecipe drec in MixingRecipeRegistry.Registry.KneadingRecipes)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);

                    drec.ToBytes(writer);

                    string value = Ascii85.Encode(ms.ToArray());
                    drecipes.Add(value);
                }
            }

            foreach (CookingRecipe crec in MixingRecipeRegistry.Registry.MixingRecipes)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);

                    crec.ToBytes(writer);

                    string value = Ascii85.Encode(ms.ToArray());
                    crecipes.Add(value);
                }
            }

            foreach (SimmerRecipe srec in MixingRecipeRegistry.Registry.SimmerRecipes)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);

                    srec.ToBytes(writer);

                    string value = Ascii85.Encode(ms.ToArray());
                    srecipes.Add(value);
                }
            }

            return new RecipeUpload()
            {
                dvalues = drecipes,
                cvalues = crecipes,
                svalues = srecipes
            };
        }


        #endregion
    }
}
*/
