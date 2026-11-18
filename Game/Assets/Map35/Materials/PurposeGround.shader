Shader "Anseong/PurposeGround"
{
 Properties
 {
  _GravelTex("Original metre-scale surface",2D)="white" {}
  _GravelNormal("Original normal",2D)="bump" {}
  _GravelRoughness("Original roughness",2D)="white" {}
  _RockTex("Local replacement material",2D)="white" {}
  _RockNormal("Replacement normal",2D)="bump" {}
  _RockRoughness("Replacement roughness",2D)="white" {}
  _PurposeMask("Authored repair / traffic / drainage / stock residue",2D)="black" {}
  _MaskRect("Mask world XZ origin and inverse extent",Vector)=(0,0,1,1)
  _SlabOrigin("Concrete pour origin XZ",Vector)=(0,0,0,0)
  _SlabMetres("Concrete pour bay metres",Vector)=(12,12,0,0)
  _Color("Original surface tint",Color)=(1,1,1,1)
  _RepairColor("Replacement tint",Color)=(1,1,1,1)
  _ResidueColor("Local steel / soil residue",Color)=(.27,.19,.11,1)
  _GravelMetres("Original physical repeat metres",Float)=1.4
  _RockMetres("Replacement physical repeat metres",Float)=3.5
  _Saturation("Preserved source colour",Range(0,1.4))=.92
  _SlabVariation("Pour-batch age difference",Range(0,.4))=.16
  _SlabEnabled("Concrete pour variation",Range(0,1))=1
  _SeamStrength("Supplemental joint, zero where mesh joints exist",Range(0,1))=0
  _NormalStrength("Fine normal strength",Range(0,2))=.5
  _RoughnessScale("Source roughness multiplier",Range(.5,1.5))=1.1
  _WearDarkening("Traffic compaction / rubber strength",Range(0,.6))=.3
  _Wetness("Dry-ground moisture floor",Range(0,1))=.035
  _WetGain("Authored runoff wetness",Range(0,1))=.7
  _RepairGain("Replacement albedo gain",Range(.5,1.5))=1.08
  _RepairColorBlend("Replacement colour contribution",Range(0,1))=.45
  _EdgeDust("Dry margin dust",Range(0,.4))=.1
  _SurfaceRect("Top-surface bounds XZ",Vector)=(0,0,1,1)
  _GroundSurface("Industrial ground audit marker",Float)=1
 }
 SubShader
 {
  Tags {"RenderType"="Opaque"} LOD 250
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow
  #pragma target 3.5
  sampler2D _GravelTex,_GravelNormal,_GravelRoughness,_RockTex,_RockNormal,_RockRoughness,_PurposeMask;
  float4 _MaskRect,_SlabOrigin,_SlabMetres,_SurfaceRect;
  fixed4 _Color,_RepairColor,_ResidueColor;
  half _Saturation,_SlabVariation,_SlabEnabled,_SeamStrength,_NormalStrength,_RoughnessScale,_WearDarkening,_Wetness,_WetGain,_RepairGain,_RepairColorBlend,_EdgeDust;
  float _GravelMetres,_RockMetres;
  struct Input {float3 worldPos;float3 worldNormal;INTERNAL_DATA};
  float purposeHash(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  void surf(Input IN,inout SurfaceOutputStandard o)
  {
   float3 n=normalize(WorldNormalVector(IN,float3(0,0,1)));
   float top=smoothstep(.5,.92,n.y);
   float2 xz=IN.worldPos.xz;
   float2 maskUV=(xz-_MaskRect.xy)*_MaskRect.zw;
   float within=step(0,maskUV.x)*step(0,maskUV.y)*step(maskUV.x,1)*step(maskUV.y,1);
   float4 purpose=tex2D(_PurposeMask,saturate(maskUV))*within*top;
   // Only actual flat ground is assigned this shader. Vertical foundation lips
   // use projected elevation instead of stretching the entire top photograph.
   float2 side=lerp(IN.worldPos.xy,IN.worldPos.zy,step(abs(n.z),abs(n.x)));
   float2 samplePoint=lerp(side,xz,top);
   float2 baseUV=samplePoint/max(_GravelMetres,.1);
   float2 repairUV=(samplePoint+float2(13.17,5.71))/max(_RockMetres,.1);
   float3 original=tex2D(_GravelTex,baseUV).rgb*_Color.rgb;
   float3 replacement=tex2D(_RockTex,repairUV).rgb*_RepairColor.rgb*_RepairGain;
   // Concrete030's broad source bands must not read as a painted blue square.
   // Keep local aggregate continuity; normal/roughness still use the repair mask.
   replacement=lerp(original,replacement,_RepairColorBlend);
   // Aggregate stays at its real scale; only the paint/repair edge is broken by
   // a small amount of existing grain. There is no global cloudy-noise layer.
   float grain=dot(tex2D(_GravelTex,baseUV*.63+17.3).rgb,float3(.2126,.7152,.0722));
   float repair=saturate(purpose.r*(.93+grain*.18));
   float wear=saturate(purpose.g*(.82+grain*.38));
   float residue=saturate(purpose.a*(.77+grain*.46));
   float wet=saturate(_Wetness+purpose.b*_WetGain);
   float2 bay=max(_SlabMetres.xy,float2(1,1));
   float2 grid=(xz-_SlabOrigin.xy)/bay;
   float2 cell=floor(grid);
   // Adjacent pours share a construction batch. Small individual differences
   // avoid an alternating checkerboard, and repairs override the aged batch.
   float batch=purposeHash(floor(cell/float2(3,2))+.217);
   float individual=purposeHash(cell+29.1);
   float age=1+((batch-.5)*.80+(individual-.5)*.20)*_SlabVariation*_SlabEnabled*top*(1-repair*.85);
   float2 seam2=min(frac(grid),1-frac(grid))*bay;
   float seamDistance=min(seam2.x,seam2.y);
   // A 32-mm joint must lose screen coverage with distance. Expanding its AA
   // footprint without scaling coverage produced metre-wide grid lines in K1.
   // Exterior aprons already have mesh joints, so their supplemental value is 0.
   float aa=max(fwidth(seamDistance),.001);
   float coverage=saturate(.032/max(aa,.032));
   float seam=(1-smoothstep(0,.016+aa*.5,seamDistance))*coverage*_SlabEnabled*_SeamStrength*top;
   float2 edge2=min(xz-_SurfaceRect.xy,_SurfaceRect.zw-xz);
   float edgeDistance=max(0,min(edge2.x,edge2.y));
   float dust=(1-smoothstep(.25,1.35,edgeDistance))*_EdgeDust*top*(1-wear);
   float3 albedo=lerp(original*age,replacement,repair);
   albedo*=1-wear*_WearDarkening;
   albedo=lerp(albedo,albedo*float3(.90,.78,.60)+_ResidueColor.rgb*.11,residue*.72);
   albedo=lerp(albedo,albedo*1.12+float3(.016,.014,.008),dust);
   float luminance=dot(albedo,float3(.2126,.7152,.0722));
   o.Albedo=lerp(luminance.xxx,albedo,_Saturation)*(1-seam*.12)*lerp(1,.77,wet);
   float rough=lerp(tex2D(_GravelRoughness,baseUV).r,tex2D(_RockRoughness,repairUV).r,repair);
   rough=clamp(rough*_RoughnessScale+.10-wear*.09+residue*.07,.3,.96);
   o.Smoothness=lerp(1-rough,.79,wet);
   // Source normal retains some distant response; authored albedo/roughness
   // supplies the medium-distance information rather than enlarged aggregate.
   float distanceFade=lerp(1,.25,smoothstep(100,400,distance(_WorldSpaceCameraPos,IN.worldPos)));
   float3 bn=UnpackNormal(tex2D(_GravelNormal,baseUV));
   float3 rn=UnpackNormal(tex2D(_RockNormal,repairUV));
   float2 detail=lerp(bn.xy,rn.xy,repair)*_NormalStrength*distanceFade*(1-wet*.42);
   float3 worldBump=normalize(n+float3(detail.x,0,detail.y)*top);
   float3 tangent=normalize(WorldNormalVector(IN,float3(1,0,0)));
   float3 bitangent=normalize(WorldNormalVector(IN,float3(0,1,0)));
   o.Normal=normalize(float3(dot(worldBump,tangent),dot(worldBump,bitangent),dot(worldBump,n)));
   o.Occlusion=1-seam*.06;o.Metallic=0;o.Alpha=1;
  }
  ENDCG
 }
 FallBack "Standard"
}
