Shader "Anseong/WorldWeathered" {
 Properties {
  _MainTex("Albedo",2D)="white" {}
  _Color("Tint",Color)=(1,1,1,1)
  _TileMetres("Tile metres",Float)=4
  _Exposure("Albedo gain",Range(.2,4))=1
  _Metallic("Metallic",Range(0,1))=.2
  _Glossiness("Smoothness",Range(0,1))=.22
  _Relief("Approximate texture relief",Range(0,1))=.12
 }
 SubShader {
  Tags {"RenderType"="Opaque"} LOD 250
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow
  #pragma target 3.5
  sampler2D _MainTex; fixed4 _Color; half _Metallic,_Glossiness,_Exposure,_Relief; float _TileMetres;
  struct Input {float3 worldPos;float3 worldNormal;INTERNAL_DATA};
  void surf(Input IN,inout SurfaceOutputStandard o) {
   float3 n=normalize(WorldNormalVector(IN,float3(0,0,1)));
   float3 weights=pow(abs(n),6);weights/=max(dot(weights,float3(1,1,1)),.0001);
   float3 p=IN.worldPos/max(_TileMetres,.1);
   fixed3 albedo=tex2D(_MainTex,p.zy).rgb*weights.x+tex2D(_MainTex,p.xz).rgb*weights.y+tex2D(_MainTex,p.xy).rgb*weights.z;
   o.Albedo=saturate(albedo*_Color.rgb*_Exposure);o.Metallic=_Metallic;o.Smoothness=_Glossiness;o.Alpha=1;
   // Small visual relief only; albedo is not a measured height/normal map.
   float h=dot(albedo,float3(.3,.59,.11));float3 dx=ddx(IN.worldPos),dy=ddy(IN.worldPos);
   float3 r1=cross(dy,n),r2=cross(n,dx);float det=dot(dx,r1);
   float3 bumped=normalize(abs(det)*n-sign(det)*(ddx(h)*r1+ddy(h)*r2)*_Relief);
   float3 tangent=normalize(WorldNormalVector(IN,float3(1,0,0))),bitangent=normalize(WorldNormalVector(IN,float3(0,1,0)));
   o.Normal=normalize(float3(dot(bumped,tangent),dot(bumped,bitangent),dot(bumped,n)));
  }
  ENDCG
 }
 FallBack "Standard"
}
