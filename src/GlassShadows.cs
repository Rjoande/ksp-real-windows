// The shadow other parts cast on the window glass (reflective glass only): where
// the pane is in shade, the sun leaves no glint on it. Unity works out the sun's
// shadows once per camera as a screen-sized mask from the scene depth, and at a
// window that depth is the pane. A command buffer on the sun's light hands that
// mask to the composite, which runs later in the same camera's render; nothing is
// copied. The hull pass can't use it: it runs before the camera's render begins.

using UnityEngine;
using UnityEngine.Rendering;

namespace RealWindows
{
    public static class GlassShadows
    {
        private const LightEvent Event = LightEvent.AfterScreenspaceMask;

        private static readonly int PID_Tex = Shader.PropertyToID("_RW_SunShadowTex");
        private static readonly int PID_Floor = Shader.PropertyToID("_RW_SunShadowFloor");

        private static CommandBuffer cb;
        private static Light attachedTo;
        private static float nextCheck;
        private static int lastState = -1;

        // Every frame from the flight camera's onPreCull, before its render. The mask
        // starts as "all lit": if the sun draws no shadows in this render (none in
        // range, shadows off) the light's buffer never runs and the glint stays.
        public static void BeginFrame(Light sun, bool wanted)
        {
            Shader.SetGlobalTexture(PID_Tex, Texture2D.whiteTexture);
            bool casts = wanted && sun != null && sun.isActiveAndEnabled && sun.shadows != LightShadows.None
                         && sun.shadowStrength > 0.01f && QualitySettings.shadows != ShadowQuality.Disable;
            // the mask holds 1 - shadow strength in full shade, not 0: the composite
            // spreads it back to 0-1
            Shader.SetGlobalFloat(PID_Floor, casts ? 1f - sun.shadowStrength : 0f);
            if (!casts)
            {
                Detach();
                if (wanted && lastState != 0)
                {
                    lastState = 0;
                    RWLog.Info("glass shadows off: the sun casts no shadows (light shadows {0}, quality {1})",
                        sun != null ? sun.shadows.ToString() : "none", QualitySettings.shadows);
                }
                return;
            }
            Attach(sun);
            if (lastState != 1)
            {
                lastState = 1;
                RWLog.Info("glass shadows from the sun's screen-space shadow mask (light '{0}', strength {1:F2})",
                    sun.name, sun.shadowStrength);
            }
        }

        private static void Attach(Light sun)
        {
            if (attachedTo == sun)
            {
                // another mod may clear the light's buffers: looked at every few seconds
                if (Time.unscaledTime < nextCheck)
                    return;
                nextCheck = Time.unscaledTime + 2f;
                if (HasBuffer(sun))
                    return;
                RWLog.Warn("another mod removed Real Windows' command buffer from the sun's light; added again");
            }
            Detach();
            if (cb == null)
            {
                cb = new CommandBuffer { name = "RealWindows glass shadows" };
                cb.SetGlobalTexture(PID_Tex, BuiltinRenderTextureType.CurrentActive);
            }
            sun.AddCommandBuffer(Event, cb);
            attachedTo = sun;
            nextCheck = Time.unscaledTime + 2f;
        }

        public static void Detach()
        {
            if (attachedTo != null && cb != null)
                attachedTo.RemoveCommandBuffer(Event, cb);
            attachedTo = null;
        }

        private static bool HasBuffer(Light sun)
        {
            // Unity hands back new wrapper objects, never the one added: compare names
            CommandBuffer[] bufs = sun.GetCommandBuffers(Event);
            for (int i = 0; i < bufs.Length; i++)
                if (bufs[i] != null && bufs[i].name == cb.name)
                    return true;
            return false;
        }
    }
}
