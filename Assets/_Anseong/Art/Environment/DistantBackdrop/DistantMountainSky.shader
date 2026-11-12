Shader "Anseong/DistantMountainSky" {
Properties { _SkyTop("Zenith",Color)=(.23,.51,.75,1) _Horizon("Horizon",Color)=(.73,.83,.86,1) _Mountain("Mountain",Color)=(.39,.51,.58,1) _Exposure("Brightness",Range(.5,2))=1 }
SubShader { Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" } Cull Off ZWrite Off
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "UnityCG.cginc"
struct appdata {float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID};
struct v2f {float4 pos:SV_POSITION; float3 ray:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO};
float4 _SkyTop,_Horizon,_Mountain; float _Exposure;
v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=UnityObjectToClipPos(v.vertex);o.ray=mul((float3x3)unity_ObjectToWorld,v.vertex.xyz);return o;}
float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
float fbm(float2 p){return noise(p)*.55+noise(p*2.03)*.27+noise(p*4.11)*.12+noise(p*8.17)*.06;}
float ridge(float a,float phase){return .014+.025*pow(.5+.5*sin(a*5+phase),2)+.025*pow(.5+.5*sin(a*11+phase*2),2)+.009*sin(a*23+phase)+.004*sin(a*47);}
fixed4 frag(v2f i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float3 d=normalize(i.ray);float a=atan2(d.x,d.z);float h=d.y;float3 col=lerp(_Horizon.rgb,_SkyTop.rgb,pow(saturate(h),.45));float band=smoothstep(-.035,.02,h);col=lerp(float3(.69,.76,.78),col,band);
// Periodic angular ridges close seamlessly across the panorama.
float farR=ridge(a,1.7)+.026;float nearR=ridge(a,4.2);float farMask=1-smoothstep(farR-.002,farR+.002,h);float nearMask=1-smoothstep(nearR-.0015,nearR+.0015,h);float coast=.45+.55*smoothstep(-.4,.4,cos(a+.6));farMask*=coast;nearMask*=coast;col=lerp(col,lerp(_Horizon.rgb,_Mountain.rgb,.38),farMask);float slope=.5+.5*sin(a*31+h*80);col=lerp(col,lerp(_Mountain.rgb,_Horizon.rgb,.27+slope*.1),nearMask);col=lerp(col,_Horizon.rgb,.35*(1-smoothstep(-.04,.045,h)));
// Clouds live at optical infinity: no scene geometry, no stereo disparity.
float2 uv=d.xz/max(.12,d.y)*1.1;float n=fbm(uv+float2(13,7));float cloud=smoothstep(.51,.72,n)*smoothstep(.055,.18,h);float thin=smoothstep(.60,.82,fbm(uv*2.2+31))*.17*smoothstep(.2,.6,h);float3 cloudColor=lerp(float3(.76,.82,.85),float3(.99,.99,.98),saturate((n-.48)*4));col=lerp(col,cloudColor,saturate(cloud+thin));return float4(col*_Exposure,1);}
ENDCG }
} Fallback Off }