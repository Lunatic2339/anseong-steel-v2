Shader "Anseong/GeologicalGround" {
 Properties {
  _RockTex("Rock albedo",2D)="white" {} _RockNormal("Rock normal",2D)="bump" {}
  _GravelTex("Gravel albedo",2D)="white" {} _GravelNormal("Gravel normal",2D)="bump" {}
  _RockRoughness("Rock roughness",2D)="white" {} _GravelRoughness("Gravel roughness",2D)="white" {}
  _Color("Tint",Color)=(.8,.8,.78,1)
  _RockMetres("Rock tile metres",Float)=7 _GravelMetres("Gravel tile metres",Float)=1.7
  _RockBias("Rock blend bias",Range(0,1))=0 _Wetness("Surface wetness",Range(0,1))=.1
  _NormalStrength("Normal strength",Range(0,2))=.65
  _DistantTerrain("Distant ridge treatment",Range(0,1))=0
  _GroundSurface("Industrial surface mode",Range(0,1))=0
  _SurfaceMix("Secondary repair and aggregate blend",Range(0,1))=0
  _RoughnessScale("Measured roughness multiplier",Range(.5,1.5))=1
  _AlbedoGain("Surface albedo gain",Range(.5,1.5))=1
  _SurfacePhase("Material sampling phase",Vector)=(0,0,0,0)
  _MacroContrast("Broad land variation contrast",Range(0,.4))=.035
  _ShoreWeathering("Wet tidal face",Range(0,1))=0
 }
 SubShader {
  Tags {"RenderType"="Opaque"} LOD 250
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow
  #pragma target 3.5
  sampler2D _RockTex,_RockNormal,_GravelTex,_GravelNormal,_RockRoughness,_GravelRoughness;
  fixed4 _Color; float _RockMetres,_GravelMetres; half _RockBias,_Wetness,_NormalStrength,_DistantTerrain;
  half _GroundSurface,_SurfaceMix,_RoughnessScale,_AlbedoGain;float4 _SurfacePhase;
  half _MacroContrast,_ShoreWeathering;
  struct Input {float3 worldPos;float3 worldNormal;float4 color:COLOR;INTERNAL_DATA};
  float hash21(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float noise21(float2 p) {
   float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
   return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);
  }
  float3 triColor(sampler2D tex,float3 p,float3 w) {
   return tex2D(tex,p.zy).rgb*w.x+tex2D(tex,p.xz).rgb*w.y+tex2D(tex,p.xy).rgb*w.z;
  }
  float3 triPerturb(sampler2D tex,float3 p,float3 w) {
   float3 a=UnpackNormal(tex2D(tex,p.zy)),b=UnpackNormal(tex2D(tex,p.xz)),c=UnpackNormal(tex2D(tex,p.xy));
   return float3(0,a.y,a.x)*w.x+float3(b.x,0,b.y)*w.y+float3(c.x,c.y,0)*w.z;
  }
  void surf(Input IN,inout SurfaceOutputStandard o) {
   float3 n=normalize(WorldNormalVector(IN,float3(0,0,1)));
   float3 w=pow(abs(n),4);w/=max(dot(w,float3(1,1,1)),.0001);
   float macro=noise21(IN.worldPos.xz*.012)+noise21(IN.worldPos.xz*.041)*.35;
   float rock=saturate(_RockBias+IN.color.r*.35+(1-abs(n.y))*2+(macro-.65)*.25);
   float3 samplePos=IN.worldPos;
   if (_GroundSurface>.5) {
    // Base/normal/roughness of two actual materials share the same mask. The
    // broad repair fields stop an entire yard repeating one bright tile.
    float repair=noise21(IN.worldPos.xz*.027+_SurfacePhase.xy);
    rock=_SurfaceMix*smoothstep(.30,.72,repair);
    samplePos+=_SurfacePhase.xyz;
   }
   // Distant ridges use broad, slowly warped strata. Near-field gravel and quays
   // retain the original metre scale; this is a separate material switch.
   if (_DistantTerrain>.5) {
    float2 warp=float2(noise21(samplePos.xz*.0031),noise21(samplePos.zx*.0037+17.1))*2-1;
    samplePos.xz+=warp*18;
    samplePos.y+=(noise21(IN.worldPos.xz*.0043+31)-.5)*11;
   }
   float3 rp=samplePos/max(_RockMetres,.1),gp=samplePos/max(_GravelMetres,.1);
   float3 albedo=lerp(triColor(_GravelTex,gp,w),triColor(_RockTex,rp,w),rock);
   if (_DistantTerrain>.5) {
    // Incommensurate broad sampling breaks the repeated light/dark motif of
    // Rock034 without generating a texture or adding geometry/tessellation.
    float3 broad=triColor(_RockTex,rp*.371+float3(.31,.47,.19),w);
    albedo=lerp(albedo,broad,.38);
   }
   float wet=saturate(_Wetness+IN.color.g*.6*(1-step(.5,_GroundSurface)));
   wet=max(wet,_ShoreWeathering*(1-smoothstep(-.55,.65,IN.worldPos.y))*.76);
   if (_GroundSurface>.5)wet*=lerp(.58,1,noise21(IN.worldPos.xz*.071+_SurfacePhase.zx));
   float gray=dot(albedo,float3(.2126,.7152,.0722));
   o.Albedo=lerp(gray.xxx,albedo,.52)*_Color.rgb*(1+(saturate(macro)-.55)*_MacroContrast)*lerp(1,.73,wet)*_AlbedoGain;
   float rough=lerp(triColor(_GravelRoughness,gp,w).r,triColor(_RockRoughness,rp,w).r,rock);
   o.Smoothness=lerp(1-clamp(rough*_RoughnessScale,.35,.98),.73,wet);o.Metallic=0;o.Occlusion=1;o.Alpha=1;
   float detailFade=lerp(1,1-smoothstep(150,450,distance(_WorldSpaceCameraPos,IN.worldPos)),_DistantTerrain);
   detailFade*=lerp(1,1-smoothstep(70,300,distance(_WorldSpaceCameraPos,IN.worldPos)),_GroundSurface);
   float3 perturb=lerp(triPerturb(_GravelNormal,gp,w),triPerturb(_RockNormal,rp,w),rock)*_NormalStrength*detailFade;
   float3 bumped=normalize(n+perturb-n*dot(n,perturb));
   float3 tangent=normalize(WorldNormalVector(IN,float3(1,0,0))),bitangent=normalize(WorldNormalVector(IN,float3(0,1,0)));
   o.Normal=normalize(float3(dot(bumped,tangent),dot(bumped,bitangent),dot(bumped,n)));
  }
  ENDCG
 }
 FallBack "Standard"
}
