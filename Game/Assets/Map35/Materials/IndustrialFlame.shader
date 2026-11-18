Shader "Anseong/IndustrialFlame" {
 Properties {_Intensity("Intensity",Float)=1.8}
 SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
  Pass {CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   float _Intensity;
   struct a {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};struct v {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;UNITY_FOG_COORDS(1)};
   v vert(a i){v o;o.pos=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.color=i.color;UNITY_TRANSFER_FOG(o,o.pos);return o;}
   fixed4 frag(v i):SV_Target {
    float y=i.uv.y,x=(i.uv.x-.5)*2;float time=_Time.y*3;
    float wave=sin(y*15-time)*.11+sin(y*29-time*1.7)*.06;
    float width=(1-y)*.65+.04;float shape=smoothstep(0,.35,1-abs(x+wave)/width)*smoothstep(0,.035,y)*(1-smoothstep(.42,1,y));
    float flicker=.72+.28*sin(y*35-time*2+x*8);float3 c=lerp(float3(1,.47,.06),float3(.65,.055,.008),y);
    fixed4 result=fixed4(c*_Intensity,shape*flicker*i.color.a*.8);
    UNITY_APPLY_FOG(i.fogCoord,result);return result;
   }
   ENDCG}
 }
}
