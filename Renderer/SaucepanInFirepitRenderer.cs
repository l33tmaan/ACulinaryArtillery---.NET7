using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    public class SaucepanInFirepitRenderer : IInFirepitRenderer
    {
        public double RenderOrder => 0.5;
        public int RenderRange => 20;

        public float origx;
        public float origz;

        ICoreClientAPI capi;
        ItemStack stack;

        MultiTextureMeshRef saucepanRef;
        MultiTextureMeshRef topRef;
        BlockPos pos;
        float temp;

        ILoadedSound cookingSound;

        bool isInOutputSlot;
        Matrixf NewModelMat = new Matrixf();

        public SaucepanInFirepitRenderer(ICoreClientAPI capi, ItemStack stack, BlockPos pos, bool isInOutputSlot)
        {
            this.capi = capi;
            this.stack = stack;
            this.pos = pos;
            this.isInOutputSlot = isInOutputSlot;

            BlockSaucepan saucepanBlock = capi.World.GetBlock(stack.Collectible.CodeWithVariant("type", "burned")) as BlockSaucepan;

            if (stack?.Collectible.CodeWithVariant("type", "burned") == null) { saucepanBlock = capi.World.GetBlock(stack.Collectible.CodeWithVariant("metal", "")) as BlockSaucepan; }

            MeshData saucepanMesh;
            capi.Tesselator.TesselateShape(saucepanBlock, capi.Assets.TryGet("aculinaryartillery:shapes/block/" + saucepanBlock.FirstCodePart() + "/" + "empty.json").ToObject<Shape>(), out saucepanMesh); // Main Shape
            //potMesh.Rgba2 = null;
            saucepanRef = capi.Render.UploadMultiTextureMesh(saucepanMesh);

            MeshData topMesh;
            capi.Tesselator.TesselateShape(saucepanBlock, capi.Assets.TryGet("aculinaryartillery:shapes/block/" + saucepanBlock.FirstCodePart() + "/" + "lid-only.json").ToObject<Shape>(), out topMesh); // Lid
            //lidMesh.Rgba2 = null;
            topRef = capi.Render.UploadMultiTextureMesh(topMesh);
        
        }

        public void Dispose()
        {
            saucepanRef?.Dispose();
            topRef?.Dispose();

            cookingSound?.Stop();
            cookingSound?.Dispose();
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);

            prog.DontWarpVertices = 0;
            prog.AddRenderFlags = 0;
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            prog.NormalShaded = 1;
            prog.ExtraGodray = 0;
            prog.SsaoAttn = 0;
            prog.AlphaTest = 0.05f;
            prog.OverlayOpacity = 0;


            prog.ModelMatrix = NewModelMat
                .Identity()
                .Translate(pos.X - camPos.X + 0.001f, pos.Y - camPos.Y, pos.Z - camPos.Z - 0.001f)
                .Translate(0f, 1 / 16f, 0f)
                .Values
            ;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMultiTextureMesh(saucepanRef, "tex");

            if (!isInOutputSlot)
            {
                 origx = GameMath.Sin(capi.World.ElapsedMilliseconds / 300f) * 8 / 16f;
                 origz = GameMath.Cos(capi.World.ElapsedMilliseconds / 300f) * 8 / 16f;

                float cookIntensity = GameMath.Clamp((temp - 50) / 50, 0, 1);

                prog.ModelMatrix = NewModelMat
                    .Identity()
                    .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                    .Translate(0, 1f / 16f, 0)
                    .Translate(-origx, 0, -origz)
                    .RotateX(cookIntensity * GameMath.Sin(capi.World.ElapsedMilliseconds / 70f) / 100) // moving of the lid
                    .RotateZ(cookIntensity * GameMath.Sin(capi.World.ElapsedMilliseconds / 70f) / 100) //moving of the lidc
                    .Translate(origx, 0, origz)
                    .Values
                ;
                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;


                rpi.RenderMultiTextureMesh(topRef, "tex");
            }

            prog.Stop();
        }

        public void OnUpdate(float temperature)
        {
            temp = temperature;

            float soundIntensity = GameMath.Clamp((temp - 50) / 50, 0, 1);
            SetCookingSoundVolume(isInOutputSlot ? 0 : soundIntensity);
        }

        public void OnCookingComplete()
        {
            isInOutputSlot = true;
        }


        public void SetCookingSoundVolume(float volume)
        {
            if (volume > 0)
            {

                if (cookingSound == null)
                {
                    cookingSound = capi.World.LoadSound(new SoundParams()
                    {
                        Location = new AssetLocation("sounds/effect/cooking.ogg"),
                        ShouldLoop = true,
                        Position = pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                        DisposeOnFinish = false,
                        Volume = volume
                    });
                    cookingSound.Start();
                }
                else
                {
                    cookingSound.SetVolume(volume);
                }

            }
            else
            {
                if (cookingSound != null)
                {
                    cookingSound.Stop();
                    cookingSound.Dispose();
                    cookingSound = null;
                }

            }

        }
    }
}

