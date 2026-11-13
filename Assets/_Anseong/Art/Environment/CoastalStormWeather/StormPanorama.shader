Shader "Anseong/StormPanorama" {
 Properties { _SkyTex ("Storm panorama", 2D) = "grey" {} }
 SubShader {
  Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
  Cull Off ZWrite Off
  Pass {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_SkyTex); SAMPLER(sampler_SkyTex);
   struct A { float4 p:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct V { float4 p:SV_POSITION; float3 d:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
   V vert(A a) {
    V v; UNITY_SETUP_INSTANCE_ID(a); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);
    v.p=TransformObjectToHClip(a.p.xyz);
    v.d=mul((float3x3)unity_ObjectToWorld,a.p.xyz);
    return v;
   }
   half4 frag(V v):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
    float3 d=normalize(v.d);
    float2 uv=float2(atan2(d.z,d.x)*.159154943+.5,asin(clamp(d.y,-1,1))*.318309886+.5);
    return SAMPLE_TEXTURE2D(_SkyTex,sampler_SkyTex,uv);
   }
   ENDHLSL
  }
 }
}
