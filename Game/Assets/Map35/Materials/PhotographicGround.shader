Shader "Anseong/PhotographicGround"
{
 Properties
 {
  _BaseTex("Measured close surface",2D)="white" {}
  _BaseNormal("Close normal GL",2D)="bump" {}
  _BaseRoughness("Close roughness",2D)="white" {}
  _DamageTex("Scanned exposed aggregate",2D)="white" {}
  _DamageNormal("Scanned exposed aggregate normal",2D)="bump" {}
  _DamageData("Scanned roughness R / height G",2D)="gray" {}
  _MacroTex("Photographed 30 metre pavement",2D)="white" {}
  _MacroNormal("Photographed pavement normal",2D)="bump" {}
  _MacroRoughness("Photographed pavement roughness",2D)="white" {}
  _PurposeMask("Traffic / drainage / residue",2D)="black" {}
  _MaskRect("World XZ origin and inverse extent",Vector)=(0,0,1,1)
  _Tint("Surface tint",Color)=(1,1,1,1)
  _BaseMetres("Measured close tile metres",Float)=2.1
  _MacroMetres("Measured broad tile metres",Float)=30
  _Asphalt("Road photograph contribution",Range(0,1))=0
  _MacroStrength("Concrete photographic weathering",Range(0,.5))=.16
  _NormalStrength("Close normal strength",Range(0,2))=.75
  _RoughnessScale("Roughness calibration",Range(.5,1.5))=1
  _WetGain("Drainage only dampness",Range(0,1))=.4
  _GroundSurface("Industrial ground marker",Float)=1
 }
 SubShader
 {
  Tags {"RenderType"="Opaque"} LOD 300
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow
  #pragma target 3.5
  #pragma multi_compile_instancing
  sampler2D _BaseTex,_BaseNormal,_BaseRoughness,_DamageTex,_DamageNormal,_DamageData;
  sampler2D _MacroTex,_MacroNormal,_MacroRoughness,_PurposeMask;
  float4 _MaskRect;fixed4 _Tint;
  float _BaseMetres,_MacroMetres;
  half _Asphalt,_MacroStrength,_NormalStrength,_RoughnessScale,_WetGain;
  struct Input {float3 worldPos;float3 worldNormal;INTERNAL_DATA};
  float2 PhotoHash(float2 v)
  {return frac(sin(float2(dot(v,float2(127.1,311.7)),dot(v,float2(269.5,183.3))))*43758.5453);}
  void PhotoTriangle(float2 p,out float2 a,out float2 b,out float2 c,out float3 w)
  {
   float2 skew=float2(p.x-p.y*.577350269,p.y*1.154700538)*3.464101615;
   float2 id=floor(skew),f=frac(skew);
   if(f.x+f.y<1){a=id;b=id+float2(1,0);c=id+float2(0,1);w=float3(1-f.x-f.y,f.x,f.y);}
   else{a=id+1;b=id+float2(0,1);c=id+float2(1,0);w=float3(f.x+f.y-1,1-f.x,1-f.y);}
   a=PhotoHash(a);b=PhotoHash(b);c=PhotoHash(c);
  }
  void surf(Input IN,inout SurfaceOutputStandard o)
  {
   float3 n=normalize(WorldNormalVector(IN,float3(0,0,1)));
   float top=smoothstep(.45,.9,n.y);
   float2 side=lerp(IN.worldPos.xy,IN.worldPos.zy,step(abs(n.z),abs(n.x)));
   float2 p=lerp(side,IN.worldPos.xz,top);
   float2 uv=p/max(_BaseMetres,.1),dx=ddx(uv),dy=ddy(uv);
   float2 a,b,c;float3 w;PhotoTriangle(uv*.22,a,b,c,w);
   // Offset sampling preserves measured grain scale; all PBR channels share
   // identical weights. Height data is used only for the measured damage scan.
   w=w*w*w;w/=max(w.x+w.y+w.z,.00001);
   float3 base=tex2Dgrad(_BaseTex,uv+a,dx,dy).rgb*w.x+tex2Dgrad(_BaseTex,uv+b,dx,dy).rgb*w.y+tex2Dgrad(_BaseTex,uv+c,dx,dy).rgb*w.z;
   float3 bn=UnpackNormal(tex2Dgrad(_BaseNormal,uv+a,dx,dy))*w.x+UnpackNormal(tex2Dgrad(_BaseNormal,uv+b,dx,dy))*w.y+UnpackNormal(tex2Dgrad(_BaseNormal,uv+c,dx,dy))*w.z;
   float rough=tex2Dgrad(_BaseRoughness,uv+a,dx,dy).r*w.x+tex2Dgrad(_BaseRoughness,uv+b,dx,dy).r*w.y+tex2Dgrad(_BaseRoughness,uv+c,dx,dy).r*w.z;
   float2 macroUV=(p+float2(11.7,4.1))/_MacroMetres;
   float3 macro=tex2D(_MacroTex,macroUV).rgb;
   float macroLum=dot(macro,float3(.2126,.7152,.0722));
   float baseLum=dot(base,float3(.2126,.7152,.0722));
   // Concrete receives restrained weathering distribution, while roads use
   // the broad photographed albedo with real-sized aggregate superimposed.
   // Mean luminance measured from this source after sRGB-to-linear conversion.
   float3 concrete=base*lerp(1,clamp(macroLum/.1264,.65,1.35),_MacroStrength);
   // The source's blue cast and high albedo overwhelmed adjacent concrete in
   // the fixed overcast comparison. Retain photographed wear with a neutral
   // asphalt calibration; roughness/normal and measured scale stay unchanged.
   float3 road=lerp(macroLum.xxx,macro,.35)*.70*lerp(.86,1.14,saturate(baseLum*3));
   float3 color=lerp(concrete,road,_Asphalt)*_Tint.rgb;
   float2 maskUV=(IN.worldPos.xz-_MaskRect.xy)*_MaskRect.zw;
   float inside=step(0,maskUV.x)*step(0,maskUV.y)*step(maskUV.x,1)*step(maskUV.y,1);
   float4 use=tex2D(_PurposeMask,saturate(maskUV))*inside*top;
   float2 damageUV=(p+float2(6.3,8.7))/2.1;
   float2 damageData=tex2D(_DamageData,damageUV).rg;
   float damage=smoothstep(.28,.72,use.r+(damageData.g-.5)*.22)*(1-_Asphalt);
   float3 damaged=tex2D(_DamageTex,damageUV).rgb*_Tint.rgb;
   color=lerp(color,damaged,damage);
   bn=normalize(lerp(bn,UnpackNormal(tex2D(_DamageNormal,damageUV)),damage));
   rough=lerp(rough,damageData.r,damage);
   float grain=saturate(baseLum*2.4);
   float wear=use.g*(.7+.3*grain),residue=use.a*(.65+.35*grain);
   float wet=use.b*_WetGain;
   color*=1-wear*.16;
   color=lerp(color,color*float3(.76,.66,.52),residue*.52);
   color*=lerp(1,.76,wet);
   o.Albedo=color;
   rough=lerp(rough,tex2D(_MacroRoughness,macroUV).r,_Asphalt*.75);
   o.Smoothness=lerp(1-clamp(rough*_RoughnessScale,.38,.92),.6,wet);
   float3 mn=UnpackNormal(tex2D(_MacroNormal,macroUV));
   float fineFade=lerp(1,.55,smoothstep(80,300,distance(_WorldSpaceCameraPos,IN.worldPos)));
   float2 slope=bn.xy*_NormalStrength*fineFade+mn.xy*_Asphalt*.8;
   float3 worldBump=normalize(n+float3(slope.x,0,slope.y)*top*(1-wet*.3));
   float3 tangent=normalize(WorldNormalVector(IN,float3(1,0,0)));
   float3 bitangent=normalize(WorldNormalVector(IN,float3(0,1,0)));
   o.Normal=normalize(float3(dot(worldBump,tangent),dot(worldBump,bitangent),dot(worldBump,n)));
   o.Metallic=0;o.Occlusion=1;o.Alpha=1;
  }
  ENDCG
 }
 FallBack "Standard"
}
