Shader "Anseong/B02PanelSurface"
{
 Properties
 {
  _MainTex("Photographed base colour",2D)="white" {}
  _BumpMap("Matched photograph normal GL",2D)="bump" {}
  _RoughnessMap("Matched photograph roughness",2D)="white" {}
  _AOMap("Matched photograph occlusion",2D)="white" {}
  _PhotoMean("Source mean linear luminance",Float)=.1
  _Reflectance("Coating reflectance LINEAR RGB",Vector)=(.24,.28,.29,1)
  _NormalizePhoto("Normalize photograph to coating",Range(0,1))=1
  _PhotoContrast("Retained photographic luminance detail",Range(0,1))=.7
  _NormalStrength("Photographic normal strength",Range(0,2))=.7
  _RoughnessScale("Photograph roughness scale",Range(.1,2))=.65
  _RoughnessBias("Photograph roughness offset",Range(-.5,.5))=.2
  _BaseMetallic("Intact surface metalness",Range(0,1))=0
  _TileMetres("Photograph width/height metres",Float)=2.7
  _OcclusionStrength("Photograph AO strength",Range(0,1))=.4
  _MetalAlbedo("Exposed steel colour",2D)="white" {}
  _MetalNormal("Exposed steel normal GL",2D)="bump" {}
  _MetalRoughness("Exposed steel roughness",2D)="white" {}
  _MetalTileMetres("Exposed steel tile metres",Float)=1.6
  _RustAlbedo("Iron oxide colour",2D)="white" {}
  _RustNormal("Iron oxide normal GL",2D)="bump" {}
  _RustRoughness("Iron oxide roughness",2D)="white" {}
  _RustTileMetres("Oxide tile metres",Float)=1.3
  _RustGain("Oxide albedo gain",Range(.3,2))=1
  _WeatherStrength("Weather A/B: zero disables ALL weather",Range(0,1))=1
  _WearAmount("Panel age envelope",Range(0,1))=.7
  _Panelize("Subdivide large source sheets",Range(0,1))=1
  _PanelWidth("Nominal sheet width metres",Float)=1.15
  _PanelHeight("Nominal sheet length metres",Float)=100
  _ReplacementRate("Fraction of clean replacement sheets",Range(0,.5))=.16
  _PanelSeed("Material variation seed",Float)=19
  _PanelVariation("Intact coating variation",Range(0,.3))=.13
  _EdgeWidth("Corroded seam width metres",Float)=.09
  _FastenerWeather("Weather below fixings",Range(0,1))=1
  _RunoffStrength("Runoff stain strength",Range(0,1))=.25
 }
 SubShader
 {
  Tags {"RenderType"="Opaque"} LOD 300
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow vertex:B02Vertex
  #pragma target 3.5
  #pragma multi_compile_instancing
  #include "UnityStandardUtils.cginc"
  sampler2D _MainTex,_BumpMap,_RoughnessMap,_AOMap;
  sampler2D _MetalAlbedo,_MetalNormal,_MetalRoughness;
  sampler2D _RustAlbedo,_RustNormal,_RustRoughness;
  float4 _Reflectance;
  float _PhotoMean,_TileMetres,_MetalTileMetres,_RustTileMetres;
  half _NormalizePhoto,_PhotoContrast,_NormalStrength,_RoughnessScale,_RoughnessBias,_BaseMetallic,_OcclusionStrength;
  half _WeatherStrength,_WearAmount,_Panelize,_ReplacementRate,_PanelVariation,_FastenerWeather,_RunoffStrength,_RustGain;
  float _PanelWidth,_PanelHeight,_PanelSeed,_EdgeWidth;
  struct Input { float2 uv_MainTex; float4 panelData; float4 panelSeed; };
  float B02Hash(float2 p)
  {
   float3 q=frac(float3(p.xyx)*.1031);
   q+=dot(q,q.yzx+33.33);
   return frac((q.x+q.y)*q.z);
  }
  float B02Noise(float2 p)
  {
   float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
   return lerp(lerp(B02Hash(i),B02Hash(i+float2(1,0)),f.x),
               lerp(B02Hash(i+float2(0,1)),B02Hash(i+1),f.x),f.y);
  }
  void B02Vertex(inout appdata_full v,out Input o)
  {
   UNITY_INITIALIZE_OUTPUT(Input,o);
   // UV0 is metres, UV2 remains Unity lightmap UV, UV3 holds (u,v,width,height).
   o.panelData=v.texcoord2;
   o.panelSeed=v.color;
   // Missing vertex colour normally reads white. The apply pass supplies stable colour
   // seeds, which also survive static batching/LOD combining. This is only a fallback.
   if(v.color.r+v.color.g>1.998 || v.color.a<.01)
   {
    float3 origin=mul(unity_ObjectToWorld,float4(0,0,0,1)).xyz;
    o.panelSeed=float4(B02Hash(origin.xz*.137+7),B02Hash(origin.xy*.191+31),.65,1);
   }
  }
  void surf(Input IN,inout SurfaceOutputStandard o)
  {
   bool hasPanel=min(IN.panelData.z,IN.panelData.w)>.005;
   float2 dimensions=hasPanel?max(IN.panelData.zw,.02):float2(max(_PanelWidth,.2),max(_PanelHeight,.2));
   float2 full=hasPanel?saturate(IN.panelData.xy)*dimensions:IN.uv_MainTex;
   float2 count=lerp(float2(1,1),max(float2(1,1),floor(dimensions/float2(max(.2,_PanelWidth),max(.2,_PanelHeight))+.5)),_Panelize);
   float2 cellSize=dimensions/count;
   float2 cell=min(floor(full/cellSize),count-1);
   if(!hasPanel) cell=floor(full/cellSize);
   float2 p=full-cell*cellSize;
   float2 seed=cell+IN.panelSeed.rg*float2(131,197)+float2(_PanelSeed,_PanelSeed*.713);
   float panelRandom=B02Hash(seed+float2(3.1,41.7));
   float replacement=step(1-_ReplacementRate,B02Hash(seed+float2(67.2,11.4)));
   float age=_WearAmount*lerp(.18,1.35,B02Hash(seed+float2(91.7,9.2)))*(1-replacement);
   age*=lerp(.55,1.2,saturate(IN.panelSeed.b))*_WeatherStrength;

   // Translation only: all photograph channels share UVs, scale and explicit gradients.
   // Explicit gradients avoid a false coarse mip at the per-sheet phase discontinuity.
   float2 phase=float2(B02Hash(seed+17),B02Hash(seed+59));
   float2 baseUV=IN.uv_MainTex/max(.1,_TileMetres)+phase;
   float2 baseDx=ddx(IN.uv_MainTex)/max(.1,_TileMetres),baseDy=ddy(IN.uv_MainTex)/max(.1,_TileMetres);
   float3 photograph=tex2Dgrad(_MainTex,baseUV,baseDx,baseDy).rgb;
   float photoLuma=dot(photograph,float3(.2126,.7152,.0722));
   float retainedDetail=lerp(1,clamp(photoLuma/max(.01,_PhotoMean),.5,1.8),_PhotoContrast);
   // Clean replacements retain the same physical corrugation, with much less old staining.
   retainedDetail=lerp(retainedDetail,lerp(1,retainedDetail,.3),replacement);
   float panelTone=1+(panelRandom-.5)*_PanelVariation;
   float3 coating=_Reflectance.rgb*retainedDetail*panelTone;
   o.Albedo=saturate(lerp(photograph,coating,_NormalizePhoto));
   o.Normal=UnpackScaleNormal(tex2Dgrad(_BumpMap,baseUV,baseDx,baseDy),_NormalStrength);
   float rough=tex2Dgrad(_RoughnessMap,baseUV,baseDx,baseDy).r*_RoughnessScale+_RoughnessBias;
   rough+=(_WeatherStrength*(panelRandom-.5)*.08);
   o.Smoothness=1-clamp(rough,.25,.98);
   o.Metallic=_BaseMetallic;
   o.Occlusion=lerp(1,tex2Dgrad(_AOMap,baseUV,baseDx,baseDy).r,_OcclusionStrength);
   o.Alpha=1;

   if(_WeatherStrength>.001 && _WearAmount>.001)
   {
    float2 rustUV=IN.uv_MainTex/max(.1,_RustTileMetres)+phase*.73;
    float2 rustDx=ddx(IN.uv_MainTex)/max(.1,_RustTileMetres),rustDy=ddy(IN.uv_MainTex)/max(.1,_RustTileMetres);
    float3 rustColor=tex2Dgrad(_RustAlbedo,rustUV,rustDx,rustDy).rgb;
    float rustRough=tex2Dgrad(_RustRoughness,rustUV,rustDx,rustDy).r;
    // Measured pits/colour provide fine, irregular paint-loss contours. The former
    // low-frequency procedural contour made the islands read like rounded stickers.
    float rustLuma=dot(rustColor,float3(.2126,.7152,.0722));
    float breakup=clamp((rustRough-.78)*.050+(rustLuma-.0603)*.12,-.016,.016)
                 +(B02Noise(p*18+seed)-.5)*.004;
    float verticalEdge=min(p.x,cellSize.x-p.x);
    float lowerEdge=p.y,upperEdge=cellSize.y-p.y;
    float edgeWidth=max(.006,_EdgeWidth)*age;
    float seamNoise=B02Noise(p*float2(3.3,1.7)+seed*2.1);
    float seamActivity=B02Noise(p*float2(2.1,3.2)+seed+31)*.72
                      +B02Noise(p*float2(8.0,13.0)+seed+83)*.28;
    // Vary the physical width continuously, rather than intersecting two signed
    // fields: that intersection gave v3's stains blunt, rectangular end caps.
    // A finite negative margin keeps an inactive seam entirely painted, including
    // the exact edge, even after the bounded photographed breakup below.
    float seamCoverage=smoothstep(.41,.70,seamActivity);
    float seam=edgeWidth*lerp(.10,1.3,seamNoise)*seamCoverage*seamCoverage
               -verticalEdge-.020;
    float bottom=edgeWidth*lerp(.15,2.3,B02Noise(float2(p.x*3.1,seed.y)+seed.x))-lowerEdge;
    bottom=min(bottom,(B02Noise(float2(p.x*1.3,seed.y+13))-.40)*.14);
    // Mid-face failure is exceptional: only a subset of older sheets gets it. Most
    // weather is still attached to seams/fixings/lower edges, not scattered circles.
    float islandPanel=step(.78,B02Hash(seed+float2(42.3,77.1)));
    float islands=(B02Noise(p*float2(1.4,.9)+seed+71)-(.95-.04*age))*.18;
    islands=min(islands,(age-.55)*.2);
    islands=min(islands,(islandPanel-.5)*.12);

    // Fixing rows and their runoff share the same deterministic locations.
    // Geometry contract: actual fixings use U=.12/.88 and V=.04/.50/.96.
    // Age/flow may vary, but oxidation must start at a real fixing, not a random row.
    float boltRow=clamp(floor((p.y/cellSize.y-.04)/.46+.5),0,2);
    float boltHeight=(.04+.46*boltRow)*cellSize.y;
    float boltY=p.y-boltHeight;
    float boltX=min(abs(p.x-cellSize.x*.12),abs(p.x-cellSize.x*.88));
    float boltSide=step(cellSize.x*.5,p.x);
    float2 boltSeed=seed+float2(boltRow*7.9,boltSide*31.7);
    float boltRadius=(.012+.030*age)*_FastenerWeather;
    float2 boltEllipse=float2(boltX*lerp(.85,1.4,B02Hash(boltSeed+6)),
                             (boltY+.012*age)*lerp(.75,1.2,B02Hash(boltSeed+14)));
    float boltShape=lerp(.50,1.4,B02Noise(p*33+boltSeed+27));
    float bolt=boltRadius*boltShape-length(boltEllipse);
    bolt=min(bolt,(B02Hash(boltSeed+11)-.35)*.13);
    bolt=min(bolt,(_FastenerWeather-.5)*.1);
    float loss=max(max(seam,bottom),max(islands,bolt))+breakup;
    float aa=max(fwidth(loss)*.75,.0015);
    float exposed=smoothstep(-aa,aa,loss)*step(.025,age);
    // All lost coating is predominantly oxide. `loss` mixes distance-like seam
    // fields with noise fields: subtracting a fixed "rust depth" from it previously
    // made centimetres-wide dark metal borders. Do not erode this mask for a rim.
    float oxide=exposed;
    // Optional fresh metal is fractional AA edge coverage on only a few sheets,
    // never a solid continuous outline. Its maximum weight is one eighth.
    float freshEdge=exposed*(1-exposed)*.5*step(.83,B02Hash(seed+29));

    float upperRow=clamp(ceil((p.y/cellSize.y-.04)/.46),0,2);
    float fromUpperBolt=(.04+.46*upperRow)*cellSize.y-p.y;
    float runLength=lerp(.15,1.15,B02Hash(seed+upperRow+53));
    float runWidth=.018+.035*B02Hash(seed+23);
    float boltRun=(1-smoothstep(runWidth*.2,runWidth,boltX))*(1-smoothstep(.04,runLength,fromUpperBolt))*step(0,fromUpperBolt);
    float topColumns=smoothstep(.65,.86,B02Noise(float2(p.x*5+seed.x,seed.y+81)));
    float topRun=topColumns*(1-smoothstep(.05,lerp(.35,1.7,B02Hash(seed+7)),upperEdge));
    float runoff=saturate(max(boltRun*_FastenerWeather,topRun)*age)*_RunoffStrength;
    // Faint soluble oxide stains are colour only: no black border, no metallic halo,
    // and no change to coating roughness/normal outside the crisp damaged core.
    float seamStain=(1-smoothstep(edgeWidth*.3,max(.045,edgeWidth*3),verticalEdge))
                    *smoothstep(.51,.77,seamActivity)*age;
    float boltStain=(1-smoothstep(runWidth,runWidth*3+.045,boltX))
                    *(1-smoothstep(.03,runLength*.65,fromUpperBolt))*step(0,fromUpperBolt)
                    *age*_FastenerWeather;
    float stain=min(.24,runoff+seamStain*.14+boltStain*.12);
    o.Albedo=lerp(o.Albedo,o.Albedo*float3(.80,.61,.43),stain);

    float2 metalUV=IN.uv_MainTex/max(.1,_MetalTileMetres)+phase*.51;
    float2 metalDx=ddx(IN.uv_MainTex)/max(.1,_MetalTileMetres),metalDy=ddy(IN.uv_MainTex)/max(.1,_MetalTileMetres);
    float3 metalColor=tex2Dgrad(_MetalAlbedo,metalUV,metalDx,metalDy).rgb;
    float3 metalNormal=UnpackScaleNormal(tex2Dgrad(_MetalNormal,metalUV,metalDx,metalDy),.35);
    float metalRough=tex2Dgrad(_MetalRoughness,metalUV,metalDx,metalDy).r;
    float3 oxideNormal=UnpackScaleNormal(tex2Dgrad(_RustNormal,rustUV,rustDx,rustDy),.65);
    // BlendNormals retains the substrate's corrugation underneath the finer pitting.
    float3 exposedNormal=BlendNormals(o.Normal,metalNormal);
    float3 oxidizedNormal=BlendNormals(o.Normal,oxideNormal);
    o.Albedo=lerp(o.Albedo,saturate(rustColor*_RustGain),oxide);
    o.Albedo=lerp(o.Albedo,metalColor,freshEdge);
    o.Normal=normalize(lerp(o.Normal,oxidizedNormal,oxide));
    o.Normal=normalize(lerp(o.Normal,exposedNormal,freshEdge));
    o.Metallic=lerp(o.Metallic,0,oxide);
    o.Metallic=lerp(o.Metallic,.92,freshEdge);
    o.Smoothness=lerp(o.Smoothness,1-clamp(rustRough,.66,.98),oxide);
    o.Smoothness=lerp(o.Smoothness,1-clamp(metalRough,.45,.82),freshEdge);
   }
  }
  ENDCG
 }
 FallBack "Standard"
}
