// The reflective glass (glassEnvironment): what the glass reflects, and the sun's
// highlight on it. Used by the hull pass, which is drawn from a command buffer with
// no light or reflection probe of its own: the inputs are globals from
// GlassEnvironment.cs.
#ifndef RW_GLASS_INCLUDED
#define RW_GLASS_INCLUDED
#include "UnityCG.cginc"

samplerCUBE _RW_EnvCube;      // the flight reflection probe's cube map
float4 _RW_EnvCube_HDR;
float _RW_EnvSynthetic;       // 1: the gradient below instead of the cube map
float4 _RW_EnvUp;             // world, away from the planet
float _RW_EnvHorizonDip;      // sine of the angle between the horizontal and the planet's limb (0 on the ground)
float4 _RW_EnvSky;
float4 _RW_EnvHorizon;
float4 _RW_EnvGround;
float _RW_EnvLight;           // the gradient's brightness, from the sun
float _RW_EnvProbeLight;      // a factor on the cube map (1 by day)

// mip levels a reflection probe spreads its roughness over (Unity's UNITY_SPECCUBE_LOD_STEPS)
#define RW_ENV_LOD_STEPS 6

// Sky above the planet's limb, ground (or the planet seen from orbit) below, a soft
// band in between.
float3 RWSyntheticEnvironment(float3 dir)
{
    float h = dot(dir, _RW_EnvUp.xyz) + _RW_EnvHorizonDip;
    float3 sky = lerp(_RW_EnvHorizon.rgb, _RW_EnvSky.rgb, sqrt(saturate(h / (1 + _RW_EnvHorizonDip))));
    float3 ground = lerp(_RW_EnvHorizon.rgb, _RW_EnvGround.rgb, saturate(-h * 4));
    return (h >= 0 ? sky : ground) * _RW_EnvLight;
}

// The scene along dir, blurred by the glass's roughness (the same roughness-to-mip
// mapping as Unity's standard shader).
float3 RWEnvironment(float3 dir, float perceptualRoughness)
{
    float mip = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness) * RW_ENV_LOD_STEPS;
    float3 probe = DecodeHDR(texCUBElod(_RW_EnvCube, float4(dir, mip)), _RW_EnvCube_HDR) * _RW_EnvProbeLight;
    return lerp(probe, RWSyntheticEnvironment(dir), _RW_EnvSynthetic);
}

// The sun's highlight (Unity's mobile GGX approximation, Schlick's Fresnel on the
// half vector): a factor for the light's colour. n, v, l normalised.
float RWSunHighlight(float3 n, float3 v, float3 l, float perceptualRoughness, float f0)
{
    float3 h = normalize(l + v);
    float nl = saturate(dot(n, l));
    float nh = saturate(dot(n, h));
    float lh = saturate(dot(l, h));
    float roughness = max(perceptualRoughness * perceptualRoughness, 0.002);
    float a2 = roughness * roughness;
    float d = nh * nh * (a2 - 1) + 1;
    float specular = min(a2 / (max(0.1, lh * lh) * (roughness + 0.5) * d * d * 4), 100);
    float fresnel = f0 + (1 - f0) * pow(1 - lh, 5);
    return specular * fresnel * nl;
}
#endif
