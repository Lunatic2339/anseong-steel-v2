Shader "Anseong/GroundTransferTest" {
 Properties {
 _MainTex("Unique colour",2D)="white"{}
 _NormalMap("Matched normal",2D)="bump"{}
 _RoughMap("Matched roughness",2D)="white"{}
 _UVRect("Source atlas rectangle",Vector)=(0,0,1,1)
 _PBR("Use matched maps",Float)=0
 _DetailNormal("Scanned detail normal",2D)="bump"{}
 _DetailColor("Scanned micro albedo",2D)="gray"{}
 _DetailRough("Scanned detail roughness",2D)="white"{}
 _Detail("Use scanned micro surface",Float)=0
 _DetailMetres("Detail metres",Float)=1.4
 _Puddles("Sparse shallow puddles",Range(0,1))=0
 _Puddle0("Puddle 0 XZ/radii",Vector)=(0,0,0,0)
 _Puddle1("Puddle 1 XZ/radii",Vector)=(0,0,0,0)
 _Puddle2("Puddle 2 XZ/radii",Vector)=(0,0,0,0)
 _Puddle3("Puddle 3 XZ/radii",Vector)=(0,0,0,0)
 _Puddle4("Puddle 4 XZ/radii",Vector)=(0,0,0,0)
 _Puddle5("Puddle 5 XZ/radii",Vector)=(0,0,0,0)
 _Puddle6("Puddle 6 XZ/radii",Vector)=(0,0,0,0)
 _Puddle7("Puddle 7 XZ/radii",Vector)=(0,0,0,0)
 }
 SubShader { Tags {"RenderType"="Opaque"} LOD 200
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows
 #pragma target 3.0
 sampler2D _MainTex,_NormalMap,_RoughMap,_DetailNormal,_DetailRough,_DetailColor;
 float4 _UVRect;float _PBR,_Detail,_DetailMetres;
 float _Puddles;float4 _Puddle0,_Puddle1,_Puddle2,_Puddle3,_Puddle4,_Puddle5,_Puddle6,_Puddle7;
 float wetHash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float wetNoise(float2 p){float2 q=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(wetHash(q),wetHash(q+float2(1,0)),f.x),lerp(wetHash(q+float2(0,1)),wetHash(q+1),f.x),f.y);}
 float wetShape(float2 p,float4 pool,float noise){float2 q=(p-pool.xy)/max(pool.zw,.01);float radius=length(q)+(noise-.5)*.26;return 1-smoothstep(.68,1.04,radius);}
 struct Input {float2 uv_MainTex;float3 worldPos;};
 void surf(Input i,inout SurfaceOutputStandard o){
 float2 u=i.uv_MainTex*_UVRect.zw+_UVRect.xy;
 o.Albedo=tex2D(_MainTex,u).rgb;
 float3 n=lerp(float3(0,0,1),UnpackNormal(tex2D(_NormalMap,u)),_PBR);
 float rough=lerp(.8,tex2D(_RoughMap,u).r,_PBR);
 float2 d=i.worldPos.xz/_DetailMetres;
 float grain=dot(tex2D(_DetailColor,d).rgb,float3(.2126,.7152,.0722));
 float mean=dot(tex2Dlod(_DetailColor,float4(.5,.5,0,12)).rgb,float3(.2126,.7152,.0722));
 o.Albedo*=lerp(1,clamp(grain/max(mean,.01),.65,1.35),_Detail*.35);
 float3 dn=UnpackNormal(tex2D(_DetailNormal,d));
 n=normalize(float3(n.xy+dn.xy*.45*_Detail,n.z));
 rough=lerp(rough,tex2D(_DetailRough,d).r,_Detail*.6);
 if(_Puddles>.001){
 float2 p=i.worldPos.xz;float noise=wetNoise(p*.95)*.7+wetNoise(p*2.6)*.3;
 float wet=saturate(wetShape(p,_Puddle0,noise)+wetShape(p,_Puddle1,noise)+wetShape(p,_Puddle2,noise)+wetShape(p,_Puddle3,noise)+wetShape(p,_Puddle4,noise)+wetShape(p,_Puddle5,noise)+wetShape(p,_Puddle6,noise)+wetShape(p,_Puddle7,noise))*_Puddles;
 float core=smoothstep(.15,.85,wet);
 o.Albedo*=1-wet*.12;
 rough=lerp(rough,.12,core);
 n=normalize(lerp(n,float3(0,0,1),core*.94));
 }
 o.Normal=n;o.Metallic=0;o.Smoothness=1-clamp(rough,.15,.98);o.Occlusion=1;o.Alpha=1;
 }
 ENDCG
 }
 FallBack "Standard"
}
