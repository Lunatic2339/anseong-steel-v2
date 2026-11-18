Shader "Anseong/OvercastSky" {
 Properties {_Horizon("Horizon",Color)=(.34,.37,.39,1) _Zenith("Zenith",Color)=(.11,.14,.17,1)}
 SubShader { Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"} Cull Off ZWrite Off
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   fixed4 _Horizon,_Zenith;
   struct appdata {float4 vertex:POSITION;};struct v2f {float4 pos:SV_POSITION;float3 dir:TEXCOORD0;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.dir=v.vertex.xyz;return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
   float fbm(float2 p){float n=0,w=.5;for(int i=0;i<5;i++){n+=noise(p)*w;p=p*2.03+17;w*=.5;}return n;}
   fixed4 frag(v2f i):SV_Target {
    float3 d=normalize(i.dir);float h=saturate(d.y);float2 p=d.xz/(max(d.y,0)+.28)*2.6+float2(_Time.y*.002,0);
    float lower=fbm(p+fbm(p*1.8)*2),upper=fbm(p*.47+11);float cover=smoothstep(.27,.72,lower*.75+upper*.25);
    float3 base=lerp(_Horizon.rgb,_Zenith.rgb,sqrt(h));float3 cloud=lerp(float3(.12,.15,.18),float3(.42,.45,.47),cover);
    float3 c=lerp(base,cloud,saturate(h*5)*.85);c=lerp(_Horizon.rgb,c,saturate(h*8));return fixed4(c,1);
   }
   ENDCG }
 }
}
