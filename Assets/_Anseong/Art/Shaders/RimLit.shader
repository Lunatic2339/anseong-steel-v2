// 기본 렌더링(Built-in) 전용. Standard에 "밝기 올림"과 "테두리 빛"을 더한다.
// 어두운 갑옷의 적이 흐린 하늘 아래에서 검은 덩어리로 뭉개지지 않게 하고, 상태(예고·빈틈)를 테두리 색으로 알린다.
// URP 프로젝트에서는 쓰지 않는다(표면 셰이더는 URP에서 그려지지 않는다).
Shader "Anseong/RimLit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _Metallic ("Metallic", Range(0,1)) = 0.15
        _Glossiness ("Smoothness", Range(0,1)) = 0.35
        _Brighten ("Albedo boost", Range(1,5)) = 2.4
        [HDR] _RimColor ("Rim color", Color) = (0.3,0.17,0.1,1)
        _RimPower ("Rim sharpness", Range(0.5,8)) = 4.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex, _BumpMap;
        fixed4 _Color, _RimColor;
        half _Metallic, _Glossiness, _Brighten, _RimPower;
        struct Input { float2 uv_MainTex; float2 uv_BumpMap; float3 viewDir; };
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            // 질감이 거의 검어도 형태가 읽히도록 바닥 밝기를 조금 깐다.
            o.Albedo = saturate(c.rgb * _Brighten + 0.07);
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = _RimColor.rgb * pow(rim, _RimPower);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
