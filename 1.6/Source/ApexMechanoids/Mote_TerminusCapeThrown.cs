using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    public class DefModExtension_TerminusCapeThrown : DefModExtension
    {
        public string bossTexPath;
        public int settleAfterTicks = 18;
        public int destroyAfterTicks = 250;
        public int fadeOutTicks = 120;
    }

    public class Mote_TerminusCapeThrown : MoteThrown
    {
        private static Mesh mirroredMesh;

        private Color drawColor = Color.white;
        private new Graphic graphicInt;
        private bool settled;
        private bool mirrored;
        private bool useBossGraphic;
        private float lastMoveAngle;
        private int customSettleAfterTicks = -1;

        private Mesh EffectiveMirroredMesh
        {
            get
            {
                Mesh source = Graphic?.MeshAt(Rot4.North) ?? MeshPool.plane10;
                if (mirroredMesh == null || mirroredMesh.vertexCount != source.vertexCount)
                {
                    mirroredMesh = Object.Instantiate(source);
                    Vector2[] uv = source.uv;
                    Vector2[] mirroredUv = new Vector2[uv.Length];
                    for (int i = 0; i < uv.Length; i++)
                    {
                        mirroredUv[i] = new Vector2(1f - uv[i].x, uv[i].y);
                    }

                    mirroredMesh.uv = mirroredUv;
                    mirroredMesh.name = "APM_TerminusCapeThrownMirrored";
                }

                return mirroredMesh;
            }
        }

        public override Color DrawColor => drawColor;

        public override Graphic Graphic
        {
            get
            {
                if (graphicInt == null)
                {
                    if (def.graphicData == null)
                    {
                        return BaseContent.BadGraphic;
                    }

                    string texPath = def.graphicData.texPath;
                    DefModExtension_TerminusCapeThrown modExt = def.GetModExtension<DefModExtension_TerminusCapeThrown>();
                    if (useBossGraphic && modExt != null && !modExt.bossTexPath.NullOrEmpty())
                    {
                        texPath = modExt.bossTexPath;
                    }

                    Color color = useBossGraphic ? Color.white : drawColor;
                    Vector2 drawSize = def.graphicData.drawSize;
                    Shader shader = def.graphicData.shaderType?.Shader ?? ShaderDatabase.Transparent;
                    graphicInt = GraphicDatabase.Get<Graphic_Mote>(texPath, shader, drawSize, color);
                }

                return graphicInt;
            }
        }

        private float FadeAlpha
        {
            get
            {
                DefModExtension_TerminusCapeThrown modExt = def.GetModExtension<DefModExtension_TerminusCapeThrown>();
                if (modExt == null)
                {
                    return 1f;
                }

                int fadeOutTicks = Mathf.Max(modExt.fadeOutTicks, 1);
                int ageTicks = Find.TickManager.TicksGame - spawnedTick;
                int fadeStartTick = Mathf.Max(modExt.destroyAfterTicks - fadeOutTicks, 0);
                if (ageTicks <= fadeStartTick)
                {
                    return 1f;
                }

                return Mathf.Clamp01((modExt.destroyAfterTicks - ageTicks) / (float)fadeOutTicks);
            }
        }

        public void Launch(Vector3 position, float moveAngle, float moveSpeed, float startRotation, float startRotationRate, Color colorOne, bool mirrored, bool useBossGraphic, int settleAfterTicksOverride)
        {
            exactPosition = position;
            exactRotation = startRotation;
            rotationRate = startRotationRate;
            lastMoveAngle = moveAngle;
            drawColor = colorOne;
            this.mirrored = mirrored;
            this.useBossGraphic = useBossGraphic;
            customSettleAfterTicks = settleAfterTicksOverride;
            graphicInt = null;
            SetVelocity(moveAngle, moveSpeed);
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Graphic graphic = Graphic;
            if (graphic == null)
            {
                return;
            }

            Vector3 pos = drawLoc;
            pos.y = def.Altitude;
            Vector2 drawSize = def.graphicData?.drawSize ?? Vector2.one;
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(exactRotation, Vector3.up), new Vector3(drawSize.x, 1f, drawSize.y));
            Mesh mesh = mirrored ? EffectiveMirroredMesh : graphic.MeshAt(Rot4.North);
            Material fadedMat = graphic.MatAt(Rot4.North, this);
            if (FadeAlpha < 0.999f)
            {
                fadedMat = FadedMaterialPool.FadedVersionOf(fadedMat, FadeAlpha);
            }

            GenDraw.DrawMeshNowOrLater(mesh, matrix, fadedMat, false);
        }

        public override void Tick()
        {
            base.Tick();

            DefModExtension_TerminusCapeThrown modExt = def.GetModExtension<DefModExtension_TerminusCapeThrown>();
            if (modExt == null)
            {
                return;
            }

            int settleAfterTicks = customSettleAfterTicks >= 0 ? customSettleAfterTicks : modExt.settleAfterTicks;
            int ageTicks = Find.TickManager.TicksGame - spawnedTick;
            if (!settled && ageTicks >= settleAfterTicks)
            {
                settled = true;
                SetVelocity(lastMoveAngle, 0f);
                rotationRate = 0f;
            }

            if (ageTicks >= modExt.destroyAfterTicks)
            {
                Destroy();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref drawColor, nameof(drawColor), Color.white);
            Scribe_Values.Look(ref settled, nameof(settled));
            Scribe_Values.Look(ref mirrored, nameof(mirrored));
            Scribe_Values.Look(ref useBossGraphic, nameof(useBossGraphic));
            Scribe_Values.Look(ref lastMoveAngle, nameof(lastMoveAngle));
            Scribe_Values.Look(ref customSettleAfterTicks, nameof(customSettleAfterTicks), -1);
        }
    }
}