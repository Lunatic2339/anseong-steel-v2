// 도형(상자·원통·공)으로 조립한 맵 소품(정비고·크레인·컨베이어·사일로)에 입히는 낡은 도장 강철(11/13 "도형 소품의 재질이 싸 보인다").
// 텍스처 없이 그린다: 패널 이음매·패널마다 다른 색, 모서리 도장 벗겨짐(상자만), 녹 얼룩, 빗물 자국, 아래쪽 때, 사선 경고 띠, 해 반사.
// 무늬는 물체 좌표(m)로 그려서 문·크레인처럼 움직이는 소품에서도 미끄러지지 않는다. 크기(scale)가 달라도 무늬 크기는 같다.
// 주의: 정적·동적 배칭으로 메시가 합쳐지면 물체 좌표가 사라진다. 소품은 Static을 켜지 않는다(SRP Batcher로 묶인다).
// 조명은 외벽 셰이더(IndustrialFacade)와 같은 방식(환경광 + 해 + 그림자 + 안개)에 반사만 더했다.
Shader "Anseong/IndustrialProp" {
Properties {
    _BaseColor("Paint", Color) = (.5,.54,.57,1)
    _Metallic("Metallic", Range(0,1)) = .4
    _Smoothness("Smoothness", Range(0,1)) = .35
    _PanelSize("Panel size (m)", Float) = 6
    _SeamStrength("Panel seam strength", Range(0,.8)) = .3
    _PanelVariation("Panel tone variation", Range(0,.4)) = .08
    _WearWidth("Edge wear width (m, boxes only)", Float) = .35
    _WearStrength("Edge wear strength", Range(0,1)) = .5
    _WearColor("Bare metal", Color) = (.62,.63,.65,1)
    _RustColor("Rust", Color) = (.36,.17,.09,1)
    _RustAmount("Rust amount", Range(0,1)) = .25
    _StreakStrength("Rain streak strength", Range(0,.6)) = .2
    _GrimeHeight("Ground grime height (m)", Float) = 6
    _GrimeStrength("Ground grime strength", Range(0,.6)) = .25
    _Stripe("Hazard stripes (0 or 1)", Range(0,1)) = 0
    _StripeSize("Stripe period (m)", Float) = 3
    _StripeColor("Stripe colour", Color) = (.07,.07,.08,1)
    _Round("Round mesh: cylinder or sphere (0 or 1)", Range(0,1)) = 0
}
SubShader { Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
Pass { Name "PropForward" Tags {"LightMode"="UniversalForward"} HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#pragma multi_compile_fog
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _BaseColor, _WearColor, _RustColor, _StripeColor;
float _Metallic, _Smoothness, _PanelSize, _SeamStrength, _PanelVariation, _WearWidth, _WearStrength, _RustAmount;
float _StreakStrength, _GrimeHeight, _GrimeStrength, _Stripe, _StripeSize, _Round;
CBUFFER_END
struct A { float4 p : POSITION; float3 n : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct V
{
    float4 p : SV_POSITION;
    float3 world : TEXCOORD0;
    float3 normal : TEXCOORD1;
    float3 local : TEXCOORD2;       // 물체 좌표 × 크기 = 물체 축을 따라 잰 m
    float3 normalOS : TEXCOORD3;
    float3 halfSize : TEXCOORD4;    // 상자 반 크기(m)
    half fog : TEXCOORD5;
    UNITY_VERTEX_OUTPUT_STEREO
};
V vert(A a)
{
    V o;
    UNITY_SETUP_INSTANCE_ID(a);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float4x4 m = GetObjectToWorldMatrix();
    float3 scale = float3(length(float3(m._m00, m._m10, m._m20)), length(float3(m._m01, m._m11, m._m21)), length(float3(m._m02, m._m12, m._m22)));
    o.world = TransformObjectToWorld(a.p.xyz);
    o.p = TransformWorldToHClip(o.world);
    o.normal = TransformObjectToWorldNormal(a.n);
    o.local = a.p.xyz * scale;
    o.normalOS = a.n;
    o.halfSize = scale * 0.5;
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
    float3 an = abs(normalize(i.normalOS));
    float3 p = i.local;
    float3 hs = i.halfSize;
    float wall = 1 - smoothstep(0.45, 0.8, abs(n.y));

    // 면 위 좌표(m)와 가장 가까운 모서리까지 거리(m).
    float2 uv;
    float edgeDistance = 1000;
    if (_Round > 0.5)
    {
        // 원통·공: 옆면은 둘레 길이 × 높이, 뚜껑은 평면. 모서리 벗겨짐은 없다.
        float2 around = float2(atan2(p.z, p.x) * hs.x, p.y);
        uv = an.y > 0.7 ? p.xz : around;
    }
    else if (an.x > an.y && an.x > an.z)
    {
        uv = p.zy;
        float2 e = hs.zy - abs(p.zy);
        edgeDistance = min(e.x, e.y);
    }
    else if (an.y > an.z)
    {
        uv = p.xz;
        float2 e = hs.xz - abs(p.xz);
        edgeDistance = min(e.x, e.y);
    }
    else
    {
        uv = p.xy;
        float2 e = hs.xy - abs(p.xy);
        edgeDistance = min(e.x, e.y);
    }

    float3 albedo = _BaseColor.rgb;

    // 사선 경고 띠(멀어지면 평균색으로 섞인다).
    float t = (uv.x + uv.y) / max(0.2, _StripeSize);
    float tri = abs(frac(t) - 0.5) * 2;
    float tw = clamp(fwidth(t) * 2, 0.001, 0.5);
    albedo = lerp(albedo, _StripeColor.rgb, smoothstep(0.5 - tw, 0.5 + tw, tri) * step(0.5, _Stripe));

    // 패널 이음매 + 패널마다 조금 다른 색.
    float ps = max(0.5, _PanelSize);
    float2 panel = uv / ps;
    float2 pe = min(frac(panel), 1 - frac(panel)) * ps;
    float d = min(pe.x, pe.y);
    float aa = max(fwidth(d), 0.005);
    float seamWidth = max(0.08, 0.02 * ps);
    float seam = 1 - smoothstep(seamWidth - aa, seamWidth + aa, d);
    float panelFade = 1 - saturate(max(fwidth(panel.x), fwidth(panel.y)) * 0.7);
    albedo *= 1 - seam * _SeamStrength * panelFade;
    albedo *= 1 + (hash(floor(panel)) - 0.5) * _PanelVariation * panelFade;

    // 모서리 도장 벗겨짐(들쭉날쭉하게) → 맨 금속.
    float grain = noise(uv * 1.3) * 0.6 + noise(uv * 5.1) * 0.4;
    float wearWidth = max(0.01, _WearWidth);
    float wear = (1 - smoothstep(0, wearWidth, edgeDistance + (grain - 0.5) * wearWidth)) * _WearStrength;
    albedo = lerp(albedo, _WearColor.rgb, wear);

    // 녹 얼룩(드문드문 큰 덩어리).
    float rustNoise = noise(uv * 0.35 + 7.7) * 0.7 + grain * 0.3;
    float rust = smoothstep(1 - _RustAmount, 1.25 - _RustAmount, rustNoise) * step(0.001, _RustAmount);
    albedo = lerp(albedo, _RustColor.rgb, rust * 0.75);

    // 빗물 자국(벽), 아래쪽 때(벽), 윗면 얼룩.
    float streak = noise(float2(uv.x * 0.5, uv.y * 0.04));
    float low = saturate(1 - i.world.y / max(0.5, _GrimeHeight));
    albedo *= 1 - (streak * _StreakStrength + low * low * _GrimeStrength) * wall;
    albedo *= 1 + (noise(uv / 9) - 0.5) * 0.25 * (1 - wall);

    // 조명: 환경광 + 해 + 그림자, 해 반사(하이라이트)와 하늘 반사.
    float metal = saturate(lerp(_Metallic, 0.9, wear) * (1 - rust));
    float smooth = saturate(lerp(_Smoothness, 0.6, wear) * (1 - rust * 0.8));
    Light sun = GetMainLight(TransformWorldToShadowCoord(i.world));
    float3 view = normalize(_WorldSpaceCameraPos - i.world);
    float ndl = saturate(dot(n, sun.direction));
    float3 direct = sun.color * (ndl * sun.shadowAttenuation);
    float3 specColor = lerp(float3(0.04, 0.04, 0.04), albedo, metal);
    float power = exp2(2 + smooth * 8);
    float3 halfDir = normalize(sun.direction + view);
    float3 highlight = specColor * direct * (pow(saturate(dot(n, halfDir)), power) * (power + 8) / 60);
    float fresnel = pow(1 - saturate(dot(n, view)), 5);
    float3 sky = max(SampleSH(reflect(-view, n)), 0) * (specColor + (1 - specColor) * fresnel * smooth * 0.5);
    float3 color = albedo * (1 - metal * 0.6) * (SampleSH(n) + direct) + highlight + sky * (0.15 + metal * 0.6);
    return half4(MixFog(color, i.fog), 1);
}
ENDHLSL }
UsePass "Universal Render Pipeline/Lit/DepthOnly"
UsePass "Universal Render Pipeline/Lit/ShadowCaster"
} Fallback "Universal Render Pipeline/Lit" }
