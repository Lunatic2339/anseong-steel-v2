Shader "Anseong/B03PhotoSurface"
{
    Properties
    {
        _MainTex ("Native-resolution wall photograph", 2D) = "white" {}
        _BaseColor ("Base colour tint", Color) = (1,1,1,1)
        _AlbedoGain ("Albedo gain", Range(0.1,4)) = 1
        [Toggle] _UsePhotoUV3 ("Wall photograph uses UV3 / TEXCOORD2", Float) = 1
        [Toggle] _UseCameraUV4 ("Reference-camera projection uses UV4 / TEXCOORD3", Float) = 0
        [Toggle] _UseReferenceVP ("Camera projection: fixed reference VP matrix from world position", Float) = 0
        _ReferenceUVScaleOffset ("Reference UV scale XY offset ZW", Vector) = (1,1,0,0)
        [Toggle] _DiagnosticUnlit ("DIAGNOSTIC ONLY: unlit photograph on geometry", Float) = 0

        _Roughness ("Surface roughness", Range(0.04,1)) = 0.8
        _Metallic ("Surface metallic", Range(0,1)) = 0
        _RoughnessMap ("Optional matching roughness, linear", 2D) = "white" {}
        _MetallicMap ("Optional matching metalness, linear", 2D) = "white" {}
        [Toggle] _UseRoughnessMap ("Use roughness map", Float) = 0
        [Toggle] _UseMetallicMap ("Use metalness map", Float) = 0
        [Toggle] _SurfaceMapsUsePhotoUV3 ("Surface maps authored for same wall UV3", Float) = 0

        _DetailNormal ("Optional matched/source normal on original UV0", 2D) = "bump" {}
        [Toggle] _UseDetailNormal ("Use MATCHED normal only", Float) = 0
        _NormalStrength ("Matched normal strength", Range(0,2)) = 0.5
        _SourceUVScaleOffset ("Original UV0 scale XY offset ZW", Vector) = (1,1,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        CGPROGRAM
        // Forward Standard keeps real lights, shadows, GI and reflection probes.
        // The optional final-colour branch is an explicitly named geometry diagnostic;
        // it is not a screen sprite, and must remain disabled for final art evaluation.
        #pragma surface surf Standard fullforwardshadows addshadow vertex:PhotoVertex finalcolor:PhotoFinalColor exclude_path:deferred
        #pragma target 3.5
        #pragma multi_compile_instancing
        #include "UnityStandardUtils.cginc"

        sampler2D _MainTex, _RoughnessMap, _MetallicMap, _DetailNormal;
        float4 _MainTex_ST, _SourceUVScaleOffset, _ReferenceUVScaleOffset;
        float4x4 _ReferenceVP;
        fixed4 _BaseColor;
        half _AlbedoGain, _UsePhotoUV3, _UseCameraUV4, _UseReferenceVP, _DiagnosticUnlit;
        half _Roughness, _Metallic, _UseRoughnessMap, _UseMetallicMap;
        half _SurfaceMapsUsePhotoUV3, _UseDetailNormal, _NormalStrength;

        struct Input
        {
            float2 originalUV;
            float2 wallUV;
            float3 referenceUVW;
            float3 worldPos;
        };

        void PhotoVertex(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.originalUV = v.texcoord.xy;
            // TEXCOORD1 / UV2 is deliberately untouched: Unity owns baked-lightmap UVs.
            o.wallUV = v.texcoord2.xy;
            // Store (viewport.u * cameraDepth, viewport.v * cameraDepth, cameraDepth).
            // Interpolating raw viewport.xy would introduce a second perspective warp.
            o.referenceUVW = v.texcoord3.xyz;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 photographUV = lerp(IN.originalUV, IN.wallUV, saturate(_UsePhotoUV3));
            if (_UseCameraUV4 > .5)
            {
                float2 referenceUV;
                if (_UseReferenceVP > .5)
                {
                    // Set with referenceCamera.projectionMatrix * worldToCameraMatrix.
                    // This is the fixed reference camera, never the current viewing camera.
                    float4 clip = mul(_ReferenceVP, float4(IN.worldPos, 1));
                    referenceUV = clip.xy / max(clip.w, .00001) * .5 + .5;
                }
                else
                    referenceUV = IN.referenceUVW.xy / max(IN.referenceUVW.z, .00001);
                photographUV = referenceUV * _ReferenceUVScaleOffset.xy + _ReferenceUVScaleOffset.zw;
            }
            photographUV = photographUV * _MainTex_ST.xy + _MainTex_ST.zw;

            // Import photographs as sRGB colour. Unity performs the sRGB -> Linear
            // conversion; do not apply gamma a second time or normalize dark colours.
            // Source images may contain baked glints/shading and are not certified PBR.
            fixed3 photograph = tex2D(_MainTex, photographUV).rgb;
            o.Albedo = saturate(photograph * _BaseColor.rgb * _AlbedoGain);

            float2 sourceUV = IN.originalUV * _SourceUVScaleOffset.xy + _SourceUVScaleOffset.zw;
            float2 surfaceUV = lerp(sourceUV, IN.wallUV, saturate(_SurfaceMapsUsePhotoUV3));
            half roughness = _Roughness;
            half metalness = _Metallic;
            if (_UseRoughnessMap > .5) roughness = tex2D(_RoughnessMap, surfaceUV).r;
            if (_UseMetallicMap > .5) metalness = tex2D(_MetallicMap, surfaceUV).r;
            o.Smoothness = 1 - clamp(roughness, .04, 1);
            o.Metallic = saturate(metalness);

            // UV0 tangent space is preserved. Never derive brick normals from colour
            // or combine a newly generated brick pattern with unrelated old brick normals.
            o.Normal = half3(0, 0, 1);
            if (_UseDetailNormal > .5)
                o.Normal = UnpackScaleNormal(tex2D(_DetailNormal, sourceUV), _NormalStrength);
            o.Occlusion = 1;
            o.Emission = 0;
            o.Alpha = 1;
            // Windows should use the separate authored glass meshes/materials. A bright
            // photographed window on this opaque surface is not functional glass.
        }

        void PhotoFinalColor(Input IN, SurfaceOutputStandard o, inout fixed4 color)
        {
            if (_DiagnosticUnlit > .5)
            {
                // Additional forward lights must not add another copy of the picture.
                #ifdef UNITY_PASS_FORWARDADD
                    color = fixed4(0, 0, 0, 0);
                #else
                    color = fixed4(o.Albedo, 1);
                #endif
            }
            // Scene fog and camera postprocessing remain external to this shader.
            // Compare diagnostics with those settings documented, not as raw pixels.
        }
        ENDCG
    }
    FallBack "Standard"
}
