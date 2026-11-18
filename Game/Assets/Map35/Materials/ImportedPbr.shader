Shader "Anseong/ImportedPbr" {
 Properties {
  _MainTex("Base color",2D)="white" {} _Color("Tint",Color)=(1,1,1,1)
  _BumpMap("Normal GL",2D)="bump" {} _NormalStrength("Normal strength",Range(0,2))=1
  _RoughnessMap("Roughness",2D)="white" {} _MetallicMap("Metalness",2D)="black" {}
  _AOMap("Occlusion",2D)="white" {} _Metallic("Metalness multiplier",Range(0,1))=1
 }
 SubShader {
  Tags {"RenderType"="Opaque"} LOD 300
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow
  #pragma target 3.5
  #include "UnityStandardUtils.cginc"
  sampler2D _MainTex,_BumpMap,_RoughnessMap,_MetallicMap,_AOMap;fixed4 _Color;half _NormalStrength,_Metallic;
  struct Input {float2 uv_MainTex;};
  void surf(Input IN,inout SurfaceOutputStandard o) {
   float2 uv=IN.uv_MainTex;
   o.Albedo=tex2D(_MainTex,uv).rgb*_Color.rgb;
   o.Normal=UnpackScaleNormal(tex2D(_BumpMap,uv),_NormalStrength);
   o.Metallic=tex2D(_MetallicMap,uv).r*_Metallic;
   o.Smoothness=1-clamp(tex2D(_RoughnessMap,uv).r,.15,.98);
   o.Occlusion=lerp(1,tex2D(_AOMap,uv).r,.8);o.Alpha=1;
  }
  ENDCG
 }
 FallBack "Standard"
}
