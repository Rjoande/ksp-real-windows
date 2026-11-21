// Glass with a body of its own (reflective glass only): a dark pane lit from
// outside, under the reflection, where no interior shows through. That is every
// window with glassOpacity = ReflectionOnly, and otherwise the windows of parts
// whose interior isn't drawn (beyond maxDistance or maxInteriors), which used to
// fall back to the painted window. Such a part needs no interior, only its window
// masks, which are kept per part type once an interior of that type has been drawn.

using UnityEngine;

namespace RealWindows
{
    public static class GlassBody
    {
        public const int Transparent = 0, ReflectionOnly = 1;

        private static readonly int PID_Ids = Shader.PropertyToID("_RW_BodyIds");
        private static readonly int PID_Color = Shader.PropertyToID("_RW_BodyColor");
        private static readonly int PID_Ambient = Shader.PropertyToID("_RW_BodyAmbient");
        private static readonly int PID_SunLight = Shader.PropertyToID("_RW_SunLight");

        private static int lastCount = -1, lastOpacity = -1;
        private static float nextLog;

        // the body belongs to the reflective glass: without it the painted window stays
        public static bool Active
        {
            get { return RWSettings.glassEnvironment; }
        }

        // no interior behind any window
        public static bool AllParts
        {
            get { return Active && RWSettings.glassOpacity == ReflectionOnly; }
        }

        public static int ParseOpacity(string s)
        {
            s = s == null ? "" : s.Trim().ToLowerInvariant();
            return s == "reflectiononly" ? ReflectionOnly : Transparent;
        }

        public static string OpacityName(int opacity)
        {
            return opacity == ReflectionOnly ? "ReflectionOnly" : "Transparent";
        }

        // ids: 1 for the part ids whose panes get the body this frame
        public static void Apply(Material mat, Color sunLight, float[] ids)
        {
            mat.SetFloatArray(PID_Ids, ids);
            mat.SetColor(PID_Color, RWSettings.glassBodyColor);
            // the composite runs without a light context: the light that falls on the
            // pane is handed over (the sun's own shadow comes from GlassShadows)
            mat.SetColor(PID_Ambient, RenderSettings.ambientLight);
            mat.SetColor(PID_SunLight, sunLight);
        }

        // Logged when the picture changes, a few seconds apart at most (the count
        // moves with the camera).
        public static void Report(int count)
        {
            if ((count == lastCount && RWSettings.glassOpacity == lastOpacity) || Time.unscaledTime < nextLog)
                return;
            lastCount = count;
            lastOpacity = RWSettings.glassOpacity;
            nextLog = Time.unscaledTime + 5f;
            RWLog.Info("glass body on {0} part(s) drawn without their interior (glassOpacity {1})",
                count, OpacityName(RWSettings.glassOpacity));
        }
    }
}
