Shader "Anseong/SurfaceSoot" {
 Properties {_MainTex("Scorch alpha",2D)="white" {} _Color("Soot tint",Color)=(.12,.1,.085,.7)}
 SubShader {
  Tags {"Queue"="Transparent-10" "RenderType"="Transparent"}
  ZWrite Off Offset -1,-1
  CGPROGRAM
  #pragma surface surf Standard alpha:fade
  #pragma target 3.0
  sampler2D _MainTex;fixed4 _Color;
  struct Input {float2 uv_MainTex;};
  void surf(Input IN,inout SurfaceOutputStandard o){fixed4 t=tex2D(_MainTex,IN.uv_MainTex);o.Albedo=t.rgb*_Color.rgb;o.Smoothness=.04;o.Metallic=0;o.Alpha=t.a*_Color.a;}
  ENDCG
 }
 FallBack Off
}
