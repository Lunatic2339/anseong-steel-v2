Shader "Anseong/JuneForest" {
Properties { _SunGreen("Subtle sunlit canopy",Color)=(.30,.38,.215,1)  _NearGreen("June forest canopy",Color)=(.21,.31,.155,1) _FoothillGreen("Muted foothill green",Color)=(.17,.255,.135,1) _DistantGreen("Blue green distant hills",Color)=(.30,.36,.31,1) }
SubShader {Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
Pass {Tags {"LightMode"="UniversalForward"} HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_fog
#pragma multi_compile_instancing
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
CBUFFER_START(UnityPerMaterial) float4 _NearGreen,_FoothillGreen,_DistantGreen,_SunGreen; CBUFFER_END
struct A{float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;half fog:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.w=TransformObjectToWorld(a.p.xyz);o.n=TransformObjectToWorldNormal(a.n);o.p=TransformWorldToHClip(o.w);o.fog=ComputeFogFactor(o.p.z);return o;}
float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float noise(float2 p){float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(c),hash(c+float2(1,0)),f.x),lerp(hash(c+float2(0,1)),hash(c+1),f.x),f.y);}
half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float hills=smoothstep(4,150,i.w.y);float patch=noise(i.w.xz/105+float2(31,17));float fine=noise(i.w.xz/22+float2(5,39));float blend=smoothstep(.12,.88,patch*.7+fine*.3);half3 base=lerp(_FoothillGreen.rgb,_NearGreen.rgb,hills);half3 albedo=lerp(base,_SunGreen.rgb,blend*.72);albedo*=1+(fine-.5)*.08;float d=length(i.w.xz-_WorldSpaceCameraPos.xz);float haze=smoothstep(2800,15500,d)*.42;albedo=lerp(albedo,_DistantGreen.rgb,haze);Light sun=GetMainLight();float3 n=normalize(i.n);half3 lighting=max(SampleSH(n)+sun.color*saturate(dot(n,sun.direction)),half3(.40,.40,.40));return half4(MixFog(albedo*lighting,i.fog),1);}ENDHLSL }
UsePass "Universal Render Pipeline/Lit/DepthOnly"
} }
