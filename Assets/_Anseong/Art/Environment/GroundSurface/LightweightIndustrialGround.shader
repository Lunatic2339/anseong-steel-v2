Shader "Anseong/LightweightIndustrialGround" {
Properties { _BaseMap("Concrete texture",2D)="white" {} _BaseColor("Tint",Color)=(.78,.79,.78,1) _TileSize("Texture repeat in metres",Float)=8 _SlabSize("Concrete panel size in metres",Float)=6 _JointStrength("Panel joint strength",Range(0,.3))=.10 _DustStrength("Broad dust variation",Range(0,.3))=.10 }
SubShader { Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
Pass { Name "GroundForward" Tags {"LightMode"="UniversalForward"} HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#pragma multi_compile_fog
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
CBUFFER_START(UnityPerMaterial) float4 _BaseMap_ST,_BaseColor;float _TileSize,_SlabSize,_JointStrength,_DustStrength;CBUFFER_END
struct A {float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;half fog:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.n);o.fog=ComputeFogFactor(o.p.z);return o;}
float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float noise(float2 p){float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(cell),hash(cell+float2(1,0)),f.x),lerp(hash(cell+float2(0,1)),hash(cell+1),f.x),f.y);}
half4 frag(V i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float2 uv=i.world.xz/max(.1,_TileSize);half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb*_BaseColor.rgb;float macro=noise(i.world.xz/45);albedo*=1+(macro-.5)*_DustStrength;float2 panel=i.world.xz/max(1,_SlabSize);float2 edge=min(frac(panel),1-frac(panel))*_SlabSize;float distanceToJoint=min(edge.x,edge.y);float aa=max(fwidth(distanceToJoint),.007);float joint=1-smoothstep(.014-aa,.014+aa,distanceToJoint);float fade=1-saturate(max(fwidth(panel.x),fwidth(panel.y))*8);albedo*=1-joint*_JointStrength*fade;float3 n=normalize(i.normal);Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));half3 lighting=SampleSH(n)+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;half3 color=albedo*lighting;return half4(MixFog(color,i.fog),1);}
ENDHLSL }
UsePass "Universal Render Pipeline/Lit/DepthOnly"
UsePass "Universal Render Pipeline/Lit/ShadowCaster"
} Fallback "Universal Render Pipeline/Lit" }