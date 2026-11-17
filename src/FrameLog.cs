// Development aid, not part of the mod's features: on a key (frameLogKey) writes
// one frame's render set-up to the log. Every camera that renders in that frame, in
// the order they render, with its settings and the command buffers on each of its
// events as Unity lists them; the sun's light with the buffers on its events; and
// the window cut-outs as the main camera is about to draw them.
//
// Other mods add their buffers while a camera culls or in its OnPreRender and take
// them off in OnPostRender, so a camera is read twice: when it is about to render
// (Camera.onPreRender) and when it has (Camera.onPostRender). Where the two lists
// differ, a buffer was added or removed in between.

using System;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealWindows
{
    public static class FrameLog
    {
        private static bool hooked;
        private static bool armed;
        private static int frame;
        private static PortalPipeline pipeline;
        private static readonly StringBuilder sb = new StringBuilder();

        public static void Arm(PortalPipeline p)
        {
            pipeline = p;
            if (!hooked)
            {
                Camera.onPreRender += OnPreRender;
                Camera.onPostRender += OnPostRender;
                hooked = true;
            }
            armed = true;
            frame = Time.frameCount;
            RWLog.Info("frame log: frame {0}, {1}, shadows {2}, HDR/MSAA per camera below", frame, SystemInfo.graphicsDeviceType, QualitySettings.shadows);
        }

        public static void Unhook()
        {
            if (hooked)
            {
                Camera.onPreRender -= OnPreRender;
                Camera.onPostRender -= OnPostRender;
                hooked = false;
            }
            armed = false;
            pipeline = null;
        }

        private static bool Live()
        {
            if (!armed)
                return false;
            if (Time.frameCount != frame)
            {
                armed = false;
                RWLog.Info("frame log: end");
                return false;
            }
            return true;
        }

        private static void OnPreRender(Camera cam)
        {
            if (!Live() || cam == null)
                return;
            try
            {
                RenderTexture rt = cam.targetTexture;
                RWLog.Info("frame log: camera '{0}' about to render: depth {1}, clear {2}, mask 0x{3:X8}, {4} rendering, depth texture {5}, HDR {6}, MSAA {7}, target {8}, clip {9:G4}-{10:G6}, into texture {11}",
                    cam.name, cam.depth, cam.clearFlags, cam.cullingMask, cam.actualRenderingPath, cam.depthTextureMode,
                    cam.allowHDR, cam.allowMSAA, rt != null ? rt.name + " " + rt.width + "x" + rt.height : "screen",
                    cam.nearClipPlane, cam.farClipPlane, cam.forceIntoRenderTexture);
                Buffers(cam);
                if (IsMain(cam))
                {
                    Sun("about to render");
                    if (pipeline != null)
                        pipeline.LogCutouts();
                }
            }
            catch (Exception e)
            {
                RWLog.Warn("frame log: {0}", e.Message);
            }
        }

        private static void OnPostRender(Camera cam)
        {
            if (!Live() || cam == null)
                return;
            try
            {
                RWLog.Info("frame log: camera '{0}' rendered", cam.name);
                Buffers(cam);
                if (IsMain(cam))
                    Sun("rendered");
            }
            catch (Exception e)
            {
                RWLog.Warn("frame log: {0}", e.Message);
            }
        }

        private static bool IsMain(Camera cam)
        {
            return FlightCamera.fetch != null && cam == FlightCamera.fetch.mainCamera;
        }

        private static void Buffers(Camera cam)
        {
            foreach (CameraEvent evt in Enum.GetValues(typeof(CameraEvent)))
            {
                CommandBuffer[] bufs = cam.GetCommandBuffers(evt);
                if (bufs == null || bufs.Length == 0)
                    continue;
                RWLog.Info("    {0}: {1}", evt, Names(bufs));
            }
        }

        // The sun's colour here against the one the passes read in onPreCull tells
        // whether another mod's change of it (Scatterer's extinction) came before or after.
        private static void Sun(string when)
        {
            Light sun = global::Sun.Instance != null ? global::Sun.Instance.sunLight : null;
            if (sun == null)
            {
                RWLog.Info("  sun: none");
                return;
            }
            Color now = sun.color * sun.intensity;
            Color read = pipeline != null ? pipeline.lastSunColour : Color.clear;
            RWLog.Info("  sun '{0}' ({1}): colour x intensity ({2:F3}, {3:F3}, {4:F3}), read by the passes as ({5:F3}, {6:F3}, {7:F3}), shadows {8}, strength {9:F2}",
                sun.name, when, now.r, now.g, now.b, read.r, read.g, read.b, sun.shadows, sun.shadowStrength);
            foreach (LightEvent evt in Enum.GetValues(typeof(LightEvent)))
            {
                CommandBuffer[] bufs = sun.GetCommandBuffers(evt);
                if (bufs == null || bufs.Length == 0)
                    continue;
                RWLog.Info("    {0}: {1}", evt, Names(bufs));
            }
        }

        private static string Names(CommandBuffer[] bufs)
        {
            sb.Length = 0;
            for (int i = 0; i < bufs.Length; i++)
            {
                if (i > 0) sb.Append(" | ");
                sb.Append(bufs[i] != null ? bufs[i].name : "(null)");
            }
            return sb.ToString();
        }
    }
}
