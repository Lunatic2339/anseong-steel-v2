Shader "Anseong/PhotographicFacade" {
 Properties {
  _PhotoMean("Measured source mean linear luminance",Float)=.18
  _Reflectance("Target coating linear reflectance",Vector)=(.25,.27,.29,1)
  _NormalizePhoto("Normalize photographed coating",Range(0,1))=0
  _PhotoColorRetention("Retain photograph colour in coating",Range(0,1))=.28
  _RoughnessBias("Roughness calibration offset",Range(-.4,.6))=0
  _MainTex("Base color",2D)="white" {} _Color("Tint",Color)=(1,1,1,1)
  _BumpMap("Normal GL",2D)="bump" {} _NormalStrength("Normal strength",Range(0,2))=1
  _RoughnessMap("Roughness linear",2D)="white" {} _MetallicMap("Metalness linear",2D)="black" {}
  _AOMap("Occlusion linear",2D)="white" {} _OcclusionStrength("Occlusion strength",Range(0,1))=.8
  _TileMetres("Physical tile size",Float)=3 _Metallic("Metalness multiplier",Range(0,1))=1
  _RoughnessScale("Roughness scale",Range(.3,1.5))=1
  _Saturation("Base color saturation",Range(0,1))=1 _AlbedoGain("Base color gain",Range(.2,2))=1
  _SurfaceCalm("Reduce repeated scratch contrast",Range(0,1))=0
  _MacroStrength("Broad surface variation",Range(0,.3))=0
  _TileAspect("Tile width height ratio",Vector)=(1,1,1,1) _RoughnessFloor("Minimum roughness",Range(0,1))=.12
  _GrimeStrength("Low wall and rain weathering",Range(0,1))=0
  _WearAmount("Local paint loss and oxidation",Range(0,1))=0
  _WearSeed("Local wear pattern seed",Float)=0
  _WearEdgeWidth("Exposed seam width in metres",Range(.01,.5))=.14
  _RustAlbedo("Rust base color",2D)="white" {}
  _RustNormal("Rust normal GL",2D)="bump" {}
  _RustRoughness("Rust roughness linear",2D)="white" {}
  _RustTileMetres("Rust detail tile metres",Float)=2
  _RustNormalStrength("Rust normal strength",Range(0,2))=.65
  _RustTint("Rust tint",Color)=(.85,.8,.72,1)
 }
 SubShader {
  Tags {"RenderType"="Opaque"} LOD 300
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow vertex:surfaceVertex
  #pragma target 3.5
  #pragma multi_compile_instancing
  #include "UnityStandardUtils.cginc"
  sampler2D _MainTex,_BumpMap,_RoughnessMap,_MetallicMap,_AOMap;
  sampler2D _RustAlbedo,_RustNormal,_RustRoughness;
  float _PhotoMean;float4 _Reflectance;half _NormalizePhoto,_PhotoColorRetention,_RoughnessBias;
  fixed4 _Color; half _NormalStrength,_Metallic,_RoughnessScale,_OcclusionStrength,_Saturation,_AlbedoGain,_SurfaceCalm,_MacroStrength,_RoughnessFloor,_GrimeStrength;float _TileMetres;float4 _TileAspect;
  fixed4 _RustTint;
  half _WearAmount,_RustNormalStrength;
  float _WearSeed,_WearEdgeWidth,_RustTileMetres;
  struct Input {float2 uv_MainTex;float3 worldPos;float4 panelData;};
  void surfaceVertex(inout appdata_full v,out Input o) {
   UNITY_INITIALIZE_OUTPUT(Input,o);
   // UV3 / TEXCOORD2: normalized face coordinates, followed by its physical width/height.
   // TEXCOORD1 remains available to Unity's baked lightmap UVs.
   o.panelData=v.texcoord2;
  }
  float surfaceHash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
  float surfaceNoise(float3 p){float3 q=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(surfaceHash(q),surfaceHash(q+float3(1,0,0)),f.x),lerp(surfaceHash(q+float3(0,1,0)),surfaceHash(q+float3(1,1,0)),f.x),f.y),lerp(lerp(surfaceHash(q+float3(0,0,1)),surfaceHash(q+float3(1,0,1)),f.x),lerp(surfaceHash(q+float3(0,1,1)),surfaceHash(q+1),f.x),f.y),f.z);}
  float2 localOxidationMask(float3 worldPosition,float4 panel,float textureBreakup) {
   float amount=saturate(_WearAmount);
   float3 seed=float3(_WearSeed*.731+7.3,_WearSeed*.419+19.1,_WearSeed*.577+3.7);
   // Rare isolated chips supplement seams. Large cloud-shaped islands must not be the
   // dominant explanation for damage on every otherwise intact cladding panel.
   float patchField=surfaceNoise(worldPosition/3.7+seed)*.74
                   +surfaceNoise(worldPosition/1.15+seed.yzx)*.26;
   float threshold=.93-.08*amount;
   // A signed paint-loss field, not a wide opacity blend. The photographed surface breaks
   // up only the boundary; intact areas remain intact rather than receiving uniform speckles.
   // The scan drives the chipped contour; these subordinate centimetre-scale irregularities
   // prevent the low-frequency seed from leaving round, sticker-like islands.
   float smallChips=(surfaceNoise(worldPosition*6+seed.zxy)-.5)*.08;
   float fineChips=(surfaceNoise(worldPosition*21+seed.yzx)-.5)*.025;
   float boundaryBreakup=textureBreakup*.09+smallChips*.6+fineChips;
   float patchFieldSigned=patchField-threshold+boundaryBreakup;
   float seamFieldSigned=-1,runoffWear=0;
   if(min(panel.z,panel.w)>.001) {
    float2 face=saturate(panel.xy);
    float2 edge=min(face,1-face)*panel.zw;
    float edgeDistance=min(edge.x,edge.y);
    float lowerEdge=face.y*panel.w;
    float topEdge=(1-face.y)*panel.w;
    float width=max(.01,_WearEdgeWidth);
    // Not a continuous rusty outline: breaks in the seam coating seed discontinuous corrosion.
    float seamBreak=surfaceNoise(worldPosition*.72+seed.zxy)-.60;
    float widthVariation=lerp(.15,1.85,smoothstep(.2,.8,
                         surfaceNoise(worldPosition*2.3+seed.yxz)));
    float corrodedWidth=width*widthVariation;
    float edgeBand=(corrodedWidth-edgeDistance)/max(width*2,.05);
    float lowerBand=(corrodedWidth*1.35-lowerEdge)/max(width*2,.05);
    seamFieldSigned=min(max(edgeBand,lowerBand)+boundaryBreakup,
                        seamBreak+textureBreakup*.1+smallChips*.6+fineChips*.5);
    // Narrow runoff starts at a panel's top seam and stops within 0.4-2m.
    // It does not paint full-height stripes through every plate on the building.
    float streakField=surfaceNoise(float3(worldPosition.x*1.65+seed.x,
                                          seed.y+11,worldPosition.z*1.65+seed.z));
    float streak=smoothstep(.61,.8,streakField);
    float reach=min(panel.w*.42,.4+1.6*surfaceNoise(worldPosition*.27+seed));
    runoffWear=streak*(1-smoothstep(width,max(width+.02,reach),topEdge))*.6;
   }
   // Keep the hard coating boundary antialiased to about one pixel; the old 0.095-wide
   // smoothstep blurred the transition over a substantial portion of the plate.
   float loss=max(patchFieldSigned,seamFieldSigned);
   float filterWidth=max(fwidth(loss)*.7,.0025);
   float coatingLoss=smoothstep(-filterWidth,filterWidth,loss);
   // Runoff is a weak colour stain, not partly transparent rust PBR spread over the coating.
   // Missing metadata falls back to paint-loss islands only, with no guessed object edges.
   return saturate(float2(coatingLoss,runoffWear*.24)*saturate(amount*4));
  }
  void surf(Input IN,inout SurfaceOutputStandard o) {
   float2 uv=IN.uv_MainTex/(max(.1,_TileMetres)*max(_TileAspect.xy,float2(.1,.1)));
   fixed3 baseColor=tex2D(_MainTex,uv).rgb;half gray=dot(baseColor,half3(.2126,.7152,.0722));
   baseColor=lerp(lerp(gray.xxx,baseColor,_Saturation),half3(.5,.5,.5),_SurfaceCalm);
   float macro=1+(surfaceNoise(IN.worldPos/19.0)-.5)*_MacroStrength;
   // Preserve the photographed variation without multiplying two dark colours.
   // Reflectance is a Vector property containing explicit linear RGB, avoiding
   // an additional sRGB conversion through a themed Color property.
   float3 photoDetail=lerp(gray.xxx,baseColor,_PhotoColorRetention)/max(_PhotoMean,.015);
   float3 coating=photoDetail*_Reflectance.xyz;
   float3 original=baseColor*_Color.rgb*_AlbedoGain;
   o.Albedo=saturate(lerp(original,coating,_NormalizePhoto)*macro);
   float runoff=surfaceNoise(float3(IN.worldPos.x*.65,IN.worldPos.y*.035,IN.worldPos.z*.65));
   float lower=(1-smoothstep(.5,6,IN.worldPos.y))*(.25+.75*surfaceNoise(IN.worldPos/4));
   float dirt=saturate(lower*.65+smoothstep(.52,.82,runoff)*.22)*_GrimeStrength;
   o.Albedo*=1-dirt;
   o.Normal=UnpackScaleNormal(tex2D(_BumpMap,uv),_NormalStrength);
   o.Metallic=tex2D(_MetallicMap,uv).r*_Metallic;
   o.Smoothness=1-clamp(tex2D(_RoughnessMap,uv).r*_RoughnessScale+_RoughnessBias,max(.12,_RoughnessFloor),.98);
   o.Occlusion=lerp(1,tex2D(_AOMap,uv).r,_OcclusionStrength);o.Alpha=1;
   // Opt-in layer: existing materials default to zero and keep their original PBR response.
   if(_WearAmount>.001) {
    float2 rustUV=IN.uv_MainTex/max(.1,_RustTileMetres);
    half rustRoughness=tex2D(_RustRoughness,rustUV).r;
    fixed3 rustSource=tex2D(_RustAlbedo,rustUV).rgb;
    // The lower-frequency sample enlarges the photographed pits to legible chip contours.
    // Centring around this rust_coarse_01 source's approximate linear means (.0603/.778)
    // avoids biasing the mask toward greater coverage merely by adding texture detail.
    float2 breakupUV=rustUV*.37+float2(_WearSeed*.021,_WearSeed*.013);
    fixed3 breakupColor=tex2D(_RustAlbedo,breakupUV).rgb;
    half breakupRoughness=tex2D(_RustRoughness,breakupUV).r;
    float textureBreakup=clamp((breakupRoughness-.778)*2.4
                       +(dot(breakupColor,half3(.2126,.7152,.0722))-.0603)*4.2,-.65,.65);
    half2 wearData=localOxidationMask(IN.worldPos,IN.panelData,textureBreakup);
    half wear=wearData.x;
    fixed3 rustColor=rustSource*_RustTint.rgb;
    half3 rustNormal=UnpackScaleNormal(tex2D(_RustNormal,rustUV),_RustNormalStrength);
    o.Albedo=lerp(o.Albedo,rustColor*macro,wearData.y*(1-wear));
    o.Albedo=lerp(o.Albedo,saturate(rustColor*macro*(1-dirt*.4)),wear);
    o.Normal=normalize(lerp(o.Normal,rustNormal,wear));
    o.Smoothness=lerp(o.Smoothness,1-clamp(rustRoughness,.62,.99),wear);
    // Iron oxide is dielectric; the substrate's metalness must not remain on the rust islands.
    o.Metallic=lerp(o.Metallic,0,wear);
   }
  }
  ENDCG
 }
 FallBack "Standard"
}
