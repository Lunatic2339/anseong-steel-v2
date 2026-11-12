// 맵의 단색 건물(텍스처 없는 URP Lit 재질)에 화면에서만 입히는 공장 외벽 무늬(11/13 "건물이 단색 상자").
// 월드 좌표로 그려 UV가 없어도 된다: 골강판 세로 골, 패널 이음매, 패널마다 조금 다른 색, 빗물 자국·아래쪽 때, 지붕·바닥 얼룩.
// 조명은 맵 바닥 셰이더(LightweightIndustrialGround)와 같은 방식(환경광 + 해 + 그림자 + 안개).
Shader "Anseong/IndustrialFacade" {
Properties {
    _BaseColor("Tint", Color) = (.6,.62,.64,1)
    _RibSize("Siding rib spacing (m)", Float) = 1.1
    _RibStrength("Siding rib strength", Range(0,.3)) = .07
    _PanelWidth("Panel width (m)", Float) = 9
    _PanelHeight("Panel height (m)", Float) = 5
    _SeamStrength("Panel seam strength", Range(0,.8)) = .38
    _PanelVariation("Panel tone variation", Range(0,.4)) = .12
    _StreakStrength("Rain streak strength", Range(0,.6)) = .2
    _GrimeHeight("Ground grime height (m)", Float) = 7
    _GrimeStrength("Ground grime strength", Range(0,.6)) = .28
    _StainStrength("Roof and floor stain strength", Range(0,.6)) = .22
    _WindowStrength("Window band strength", Range(0,1)) = .7
    _WindowRatio("Share of panels with a window", Range(0,1)) = .4
}
SubShader { Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
Pass { Name "FacadeForward" Tags {"LightMode"="UniversalForward"} HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#pragma multi_compile_fog
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _BaseColor;
float _RibSize, _RibStrength, _PanelWidth, _PanelHeight, _SeamStrength, _PanelVariation, _StreakStrength, _GrimeHeight, _GrimeStrength, _StainStrength, _WindowStrength, _WindowRatio;
CBUFFER_END
struct A { float4 p : POSITION; float3 n : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct V { float4 p : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; half fog : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
V vert(A a)
{
    V o;
    UNITY_SETUP_INSTANCE_ID(a);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.world = TransformObjectToWorld(a.p.xyz);
    o.p = TransformWorldToHClip(o.world);
    o.normal = TransformObjectToWorldNormal(a.n);
    o.fog = ComputeFogFactor(o.p.z);
    return o;
}
float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float noise(float2 p)
{
    float2 cell = floor(p), f = frac(p);
    f = f * f * (3 - 2 * f);
    return lerp(lerp(hash(cell), hash(cell + float2(1, 0)), f.x), lerp(hash(cell + float2(0, 1)), hash(cell + 1), f.x), f.y);
}
half4 frag(V i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 n = normalize(i.normal);
    float wall = 1 - smoothstep(0.45, 0.8, abs(n.y));
    // 벽을 따라가는 가로 좌표: 벽이 보는 방향에 따라 월드 X 또는 Z.
    float h = abs(n.x) > abs(n.z) ? i.world.z : i.world.x;
    float y = i.world.y;
    half3 albedo = _BaseColor.rgb;

    float ribSize = max(0.1, _RibSize);
    float ribFade = 1 - saturate(fwidth(h / ribSize) * 1.5);
    albedo *= 1 + sin(h * 6.2831853 / ribSize) * _RibStrength * ribFade * wall;

    float2 size = float2(max(1, _PanelWidth), max(1, _PanelHeight));
    float2 panel = float2(h, y) / size;
    float2 edge = min(frac(panel), 1 - frac(panel)) * size;
    float d = min(edge.x, edge.y);
    float aa = max(fwidth(d), 0.01);
    float seam = 1 - smoothstep(0.22 - aa, 0.22 + aa, d);
    float panelFade = 1 - saturate(max(fwidth(panel.x), fwidth(panel.y)) * 2.5);
    albedo *= 1 - seam * _SeamStrength * panelFade * wall;
    albedo *= 1 + (hash(floor(panel)) - 0.5) * _PanelVariation * panelFade * wall;

    // 창: 일부 패널 위쪽에 어두운 유리 띠(땅에서 4 m 위부터).
    float2 pf = frac(panel);
    float2 wd = min(pf - float2(0.14, 0.52), float2(0.86, 0.82) - pf) * size;
    float waa = max(fwidth(wd.x) + fwidth(wd.y), 0.02);
    float window = smoothstep(-waa, waa, min(wd.x, wd.y)) * step(1 - _WindowRatio, hash(floor(panel) + 17.3)) * step(6, y) * panelFade * wall * _WindowStrength;
    albedo = lerp(albedo, half3(0.07, 0.10, 0.13), window);

    float streak = noise(float2(h * 0.18, y * 0.02));
    float low = saturate(1 - y / max(0.5, _GrimeHeight));
    albedo *= 1 - (streak * _StreakStrength + low * low * _GrimeStrength) * wall;

    float stain = noise(i.world.xz / 40) * 0.65 + noise(i.world.xz / 11) * 0.35;
    albedo *= 1 + (stain - 0.5) * _StainStrength * (1 - wall);

    Light sun = GetMainLight(TransformWorldToShadowCoord(i.world));
    half3 lighting = SampleSH(n) + sun.color * saturate(dot(n, sun.direction)) * sun.shadowAttenuation;
    half3 color = albedo * lighting;
    return half4(MixFog(color, i.fog), 1);
}
ENDHLSL }
UsePass "Universal Render Pipeline/Lit/DepthOnly"
UsePass "Universal Render Pipeline/Lit/ShadowCaster"
} Fallback "Universal Render Pipeline/Lit" }
