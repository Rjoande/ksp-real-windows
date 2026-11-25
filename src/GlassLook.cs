// The look of the reflective glass (glassEnvironment): a reflection profile - how
// the pane reflects, how much it lets through and how bright its own body is - and
// a colour that tints the reflection, what shows through and the body. Both are
// picked by name in settings.cfg; GlassProfile and GlassColor nodes there add to
// the built-in ones or change them.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealWindows
{
    public static class GlassLook
    {
        public class Profile
        {
            public string name;
            public float f0 = 0.04f;              // reflectance facing the camera
            public float fresnelPower = 5f;       // how late the reflectance rises towards grazing angles
            public float smoothness = 0.85f;      // 1 = mirror-sharp reflection, pin-point glint
            public float reflection = 1f;         // x the reflectance
            public float reflectionTint = 0f;     // share of the colour in the reflection (coated glass)
            public float transmission = 0.85f;    // how much of what is behind the pane shows
            public float transmissionTint = 0.5f; // share of the colour in what shows through
            public float bodyLevel = 0.08f;       // brightness of the pane's own body in full light
        }

        public class Tint
        {
            public string name;
            public Color color = Color.white;
            public bool neutral;                  // no colour at all: the glass as Real Windows draws it
        }

        private static readonly int PID_F0 = Shader.PropertyToID("_RW_GlassF0");
        private static readonly int PID_FresnelPower = Shader.PropertyToID("_RW_GlassFresnelPower");
        private static readonly int PID_Smoothness = Shader.PropertyToID("_RW_Smoothness");
        private static readonly int PID_PaneReflection = Shader.PropertyToID("_RW_GlassRefl");
        private static readonly int PID_BodyReflection = Shader.PropertyToID("_RW_BodyRefl");
        private static readonly int PID_Hue = Shader.PropertyToID("_RW_GlassHue");

        private static readonly List<Profile> profiles = new List<Profile>();
        private static readonly List<Tint> colours = new List<Tint>();
        private static int profile, colour;

        // the body of a pane with no colour: a cool grey
        public static Color bodyTint = new Color(0.77f, 0.85f, 1f, 1f);
        // what is left of the reflection at night, when the game has faded the sun out:
        // the gradient's own floor, and a factor on the probe's image (1 = as the probe drew it)
        public static float nightReflection = 0.02f;
        public static float nightProbe = 1f;

        public static Profile Pane
        {
            get { Ensure(); return profiles[profile]; }
        }

        // A pane with nothing behind it reflects like coated glass at least: clear
        // glass over a dark body is a black hole.
        public static Profile Body
        {
            get
            {
                Ensure();
                if (profile != 0)
                    return profiles[profile];
                Profile coated = Find(profiles, "Capsule");
                return coated ?? profiles[0];
            }
        }

        public static string ProfileName
        {
            get { return Pane.name; }
        }

        public static string ColourName
        {
            get { Ensure(); return colours[colour].name; }
        }

        // the colour at full brightness: what tints the reflection and the body
        public static Color Hue
        {
            get
            {
                Ensure();
                Tint t = colours[colour];
                if (t.neutral)
                    return Color.white;
                float max = Mathf.Max(t.color.r, Mathf.Max(t.color.g, t.color.b));
                return max > 1e-4f ? new Color(t.color.r / max, t.color.g / max, t.color.b / max, 1f) : Color.white;
            }
        }

        public static Color BodyColor
        {
            get
            {
                Ensure();
                Color c = colours[colour].neutral ? bodyTint : Hue;
                float level = Body.bodyLevel;
                return new Color(c.r * level, c.g * level, c.b * level, 1f);
            }
        }

        public static string NextProfile()
        {
            Ensure();
            profile = (profile + 1) % profiles.Count;
            return profiles[profile].name;
        }

        public static string NextColour()
        {
            Ensure();
            colour = (colour + 1) % colours.Count;
            return colours[colour].name;
        }

        // Read after the plain glass keys: Clear is the glass those describe.
        public static void Load(ConfigNode n)
        {
            Reset();
            foreach (ConfigNode pn in n.GetNodes("GlassProfile"))
            {
                string name = pn.GetValue("name");
                if (string.IsNullOrEmpty(name))
                    continue;
                Profile p = Find(profiles, name.Trim());
                if (p == null)
                {
                    p = new Profile { name = name.Trim() };
                    profiles.Add(p);
                }
                RWSettings.F(pn, "f0", ref p.f0);
                RWSettings.F(pn, "fresnelPower", ref p.fresnelPower);
                RWSettings.F(pn, "smoothness", ref p.smoothness);
                RWSettings.F(pn, "reflection", ref p.reflection);
                RWSettings.F(pn, "reflectionTint", ref p.reflectionTint);
                RWSettings.F(pn, "transmission", ref p.transmission);
                RWSettings.F(pn, "transmissionTint", ref p.transmissionTint);
                RWSettings.F(pn, "bodyLevel", ref p.bodyLevel);
            }
            foreach (ConfigNode cn in n.GetNodes("GlassColor"))
            {
                string name = cn.GetValue("name");
                if (string.IsNullOrEmpty(name))
                    continue;
                Tint t = Find(colours, name.Trim());
                if (t == null)
                {
                    t = new Tint { name = name.Trim() };
                    colours.Add(t);
                }
                if (RWSettings.C(cn, "color", ref t.color))
                    t.neutral = false;
            }
            RWSettings.C(n, "glassBodyTint", ref bodyTint);
            RWSettings.F(n, "glassNightReflection", ref nightReflection);
            RWSettings.F(n, "glassNightProbe", ref nightProbe);

            string s = n.GetValue("glassProfile");
            if (!string.IsNullOrEmpty(s))
            {
                int i = profiles.FindIndex(p => string.Equals(p.name, s.Trim(), StringComparison.OrdinalIgnoreCase));
                if (i >= 0) profile = i;
                else RWLog.Warn("unknown glassProfile '{0}'", s);
            }
            s = n.GetValue("glassColor");
            if (!string.IsNullOrEmpty(s))
            {
                int i = colours.FindIndex(c => string.Equals(c.name, s.Trim(), StringComparison.OrdinalIgnoreCase));
                if (i >= 0) colour = i;
                else RWLog.Warn("unknown glassColor '{0}'", s);
            }
        }

        // The glass surface of the part drawn next in the hull pass. Without the
        // reflective glass the surface is the one Real Windows always drew.
        public static void ApplyHull(CommandBuffer cb, bool reflective, bool body)
        {
            Profile p = reflective ? (body ? Body : Pane) : null;
            cb.SetGlobalFloat(PID_F0, p != null ? p.f0 : 0.04f);
            cb.SetGlobalFloat(PID_FresnelPower, p != null ? p.fresnelPower : 5f);
            cb.SetGlobalFloat(PID_Smoothness, p != null ? p.smoothness : RWSettings.glassSmoothness);
        }

        // Reflective glass only: the composite's share of the look.
        public static void ApplyComposite(Material mat)
        {
            Profile pane = Pane, body = Body;
            mat.SetVector(PID_PaneReflection, new Vector4(pane.reflection, pane.reflectionTint, 0f, 0f));
            mat.SetVector(PID_BodyReflection, new Vector4(body.reflection, body.reflectionTint, 0f, 0f));
            mat.SetColor(PID_Hue, Hue);
            mat.SetFloat("_RW_Transmission", pane.transmission);
            // what shows through takes the colour as it is given: a dark one dims it too
            Tint t = colours[colour];
            Color filter = t.neutral ? Color.white : Color.Lerp(Color.white, t.color, pane.transmissionTint);
            Color g = RWSettings.glassTint;
            mat.SetColor("_RW_Tint", new Color(g.r * filter.r, g.g * filter.g, g.b * filter.b, 1f));
        }

        private static void Ensure()
        {
            if (profiles.Count == 0)
                Reset();
        }

        private static void Reset()
        {
            profiles.Clear();
            colours.Clear();
            profile = colour = 0;
            // Clear: the glass of the plain keys (transmission, glassReflection, glassSmoothness)
            profiles.Add(new Profile
            {
                name = "Clear",
                smoothness = RWSettings.glassSmoothness,
                reflection = RWSettings.glassReflection,
                transmission = RWSettings.transmission,
            });
            profiles.Add(new Profile
            {
                name = "Capsule", f0 = 0.1f, smoothness = 0.95f, reflectionTint = 0.7f, transmission = 0.7f,
            });
            profiles.Add(new Profile
            {
                name = "Rover", f0 = 0.05f, fresnelPower = 3f, smoothness = 0.9f, reflectionTint = 0.4f, transmission = 0.8f,
            });
            colours.Add(new Tint { name = "Neutral", neutral = true });
            colours.Add(new Tint { name = "Smoke", color = new Color(0.5f, 0.5f, 0.5f, 1f) });
            colours.Add(new Tint { name = "Gold", color = new Color(1f, 0.76f, 0.33f, 1f) });
            colours.Add(new Tint { name = "Green", color = new Color(0.45f, 0.75f, 0.5f, 1f) });
            colours.Add(new Tint { name = "Amber", color = new Color(1f, 0.6f, 0.2f, 1f) });
        }

        private static Profile Find(List<Profile> list, string name)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i].name, name, StringComparison.OrdinalIgnoreCase))
                    return list[i];
            return null;
        }

        private static Tint Find(List<Tint> list, string name)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i].name, name, StringComparison.OrdinalIgnoreCase))
                    return list[i];
            return null;
        }
    }
}
