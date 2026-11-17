// What the window glass reflects (glassEnvironment): the stock flight reflection
// probe's cube map, or a sky / horizon / ground gradient where there is no probe
// image to use. The hull pass runs from a command buffer outside any camera's
// render, where Unity binds no reflection probe (unity_SpecCube0) and no light, so
// the cube map and the gradient's inputs are handed over as globals every frame.

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealWindows
{
    public static class GlassEnvironment
    {
        public const int SourceAuto = 0, SourceProbe = 1, SourceSynthetic = 2;

        // Kerbin's sea-level air density: the gradient's full sky colour from here down
        private const double SeaLevelDensity = 1.225;

        // the gradient: sky, horizon and ground in air; black sky and the planet's
        // own colour below once the air has thinned out
        private static readonly Color Sky = new Color(0.45f, 0.65f, 1f);
        private static readonly Color Horizon = new Color(0.85f, 0.88f, 0.92f);
        private static readonly Color Ground = new Color(0.18f, 0.18f, 0.14f);
        private static readonly Color Space = new Color(0f, 0f, 0f);
        private static readonly Color Planet = new Color(0.35f, 0.5f, 0.7f);

        private static readonly int PID_Cube = Shader.PropertyToID("_RW_EnvCube");
        private static readonly int PID_CubeHDR = Shader.PropertyToID("_RW_EnvCube_HDR");
        private static readonly int PID_Synthetic = Shader.PropertyToID("_RW_EnvSynthetic");
        private static readonly int PID_Up = Shader.PropertyToID("_RW_EnvUp");
        private static readonly int PID_HorizonDip = Shader.PropertyToID("_RW_EnvHorizonDip");
        private static readonly int PID_Sky = Shader.PropertyToID("_RW_EnvSky");
        private static readonly int PID_Horizon = Shader.PropertyToID("_RW_EnvHorizon");
        private static readonly int PID_Ground = Shader.PropertyToID("_RW_EnvGround");
        private static readonly int PID_Light = Shader.PropertyToID("_RW_EnvLight");

        private static int lastUsed = -1;

        public static int ParseSource(string s)
        {
            s = s == null ? "" : s.Trim().ToLowerInvariant();
            return s == "probe" ? SourceProbe : s == "synthetic" ? SourceSynthetic : SourceAuto;
        }

        public static string SourceName(int source)
        {
            return source == SourceProbe ? "Probe" : source == SourceSynthetic ? "Synthetic" : "Auto";
        }

        // Queues the environment's globals for the hull pass drawn after this.
        public static void Apply(CommandBuffer cb, Light sun)
        {
            Texture cube = null;
            Vector4 decode = new Vector4(1f, 1f, 0f, 0f);
            if (RWSettings.glassEnvironmentSource != SourceSynthetic)
            {
                // Auto follows the game's setting: with the probe switched off its last
                // image (if it ever drew one) is stale
                bool wanted = RWSettings.glassEnvironmentSource == SourceProbe || GameSettings.REFLECTION_PROBE_REFRESH_MODE != 0;
                FlightReflectionProbe frp = FlightCamera.fetch != null ? FlightCamera.fetch.reflectionProbe : null;
                ReflectionProbe probe = frp != null ? frp.probeComponent : null;
                if (wanted && probe != null && probe.texture != null && probe.texture.dimension == TextureDimension.Cube)
                {
                    cube = probe.texture;
                    decode = probe.textureHDRDecodeValues;
                }
            }

            int used = cube != null ? SourceProbe : SourceSynthetic;
            if (used != lastUsed)
            {
                lastUsed = used;
                if (cube != null)
                    RWLog.Info("glass reflects the reflection probe ({0}px cube map)", cube.width);
                else if (RWSettings.glassEnvironmentSource == SourceSynthetic)
                    RWLog.Info("glass reflects the sky/ground gradient (source Synthetic)");
                else
                    RWLog.Info("glass reflects the sky/ground gradient (source {0}, reflection probe {1})",
                        SourceName(RWSettings.glassEnvironmentSource),
                        GameSettings.REFLECTION_PROBE_REFRESH_MODE == 0 ? "off in the game's settings" : "has no image");
            }

            if (cube != null)
            {
                cb.SetGlobalTexture(PID_Cube, cube);
                cb.SetGlobalVector(PID_CubeHDR, decode);
            }
            cb.SetGlobalFloat(PID_Synthetic, cube != null ? 0f : 1f);

            Vector3 up = Vector3.up;
            float air = 1f;
            float horizonDip = 0f;
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel != null && vessel.mainBody != null)
            {
                up = ((Vector3)(vessel.CoMD - vessel.mainBody.position)).normalized;
                air = Mathf.Sqrt(Mathf.Clamp01((float)(vessel.atmDensity / SeaLevelDensity)));
                // the planet's limb sits below the horizontal by acos(R / (R + altitude))
                double radius = vessel.mainBody.Radius;
                double ratio = radius / (radius + Math.Max(vessel.altitude, 0.0));
                horizonDip = (float)Math.Sqrt(Math.Max(0.0, 1.0 - ratio * ratio));
            }
            cb.SetGlobalVector(PID_Up, up);
            cb.SetGlobalFloat(PID_HorizonDip, horizonDip);
            cb.SetGlobalColor(PID_Sky, Color.Lerp(Space, Sky, air));
            cb.SetGlobalColor(PID_Horizon, Color.Lerp(Space, Horizon, air));
            cb.SetGlobalColor(PID_Ground, Color.Lerp(Planet, Ground, air));
            // the gradient has no light of its own: it follows the sun (which the game
            // fades out at night), never quite to black
            Color sc = sun != null ? sun.color * sun.intensity : Color.white;
            cb.SetGlobalFloat(PID_Light, Mathf.Lerp(0.1f, 1f, Mathf.Clamp01(Mathf.Max(sc.r, Mathf.Max(sc.g, sc.b)))));
        }
    }
}
