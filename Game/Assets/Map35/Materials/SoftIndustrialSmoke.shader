Shader "Anseong/SoftIndustrialSmoke" {
 Properties {_Color("Tint",Color)=(.39,.42,.44,.58) _MainTex("Optional source mask",2D)="white" {}}
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   fixed4 _Color;
   float hashSmoke(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noiseSmoke(float2 p){float2 q=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hashSmoke(q),hashSmoke(q+float2(1,0)),f.x),lerp(hashSmoke(q+float2(0,1)),hashSmoke(q+1),f.x),f.y);}
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;UNITY_FOG_COORDS(1)};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;UNITY_TRANSFER_FOG(o,o.pos);return o;}
   fixed4 frag(v2f i):SV_Target {
    float2 p=i.uv*2-1;
    float n=noiseSmoke(i.uv*4.7)*.6+noiseSmoke(i.uv*11.3)*.27+noiseSmoke(i.uv*23)*.13;
    // The outer envelope reaches zero before the billboard edge, even when a copied
    // material has no alpha texture. This is independent of the source URP shader.
    float alpha=(1-smoothstep(.52,.96,length(p)))*smoothstep(.17,.72,n)*.8;
    fixed4 c=i.color*_Color;c.rgb*=lerp(.7,1.12,n);c.a*=alpha;
    UNITY_APPLY_FOG(i.fogCoord,c);return c;
   }
   ENDCG }
 }
}
