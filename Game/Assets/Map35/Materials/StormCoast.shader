Shader "Anseong/StormCoast" {
 Properties {_Color("Deep water",Color)=(.045,.12,.15,1) _ShoreX("Quay X",Float)=430 _ReflectionCube("Cloud reflection",Cube)="" {} _UseContactColour("Mesh shoreline contact colour",Range(0,1))=0}
 SubShader {Tags {"RenderType"="Opaque"} LOD 200
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   struct appdata {float4 vertex:POSITION;fixed4 color:COLOR;};
   struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;fixed contact:TEXCOORD2;UNITY_FOG_COORDS(1)};
   fixed4 _Color;float _ShoreX,_UseContactColour;samplerCUBE _ReflectionCube;
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.contact=v.color.r*_UseContactColour;UNITY_TRANSFER_FOG(o,o.pos);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
   float wave(float2 p,float t){return noise(p*.045+float2(t*.011,t*.006))*.6+noise(p*.13+float2(-t*.021,t*.013))*.20;}
   fixed4 frag(v2f i):SV_Target {
    float2 p=i.world.xz;float t=_Time.y;float3 view=normalize(_WorldSpaceCameraPos-i.world);
    float footprint=max(length(ddx(p)),length(ddy(p)));float fade=1-smoothstep(.2,2.5,footprint);
    float2 slope=float2(wave(p+float2(.6,0),t)-wave(p-float2(.6,0),t),wave(p+float2(0,.6),t)-wave(p-float2(0,.6),t))/1.2;
    slope*=1-smoothstep(3,15,footprint);
    slope+=float2(noise(p*.4+float2(t*.08,0))-.5,noise(p*.37+float2(0,t*.05))-.5)*.025*fade;
    float3 n=normalize(float3(slope.x,1,slope.y));float fres=.025+.92*pow(1-saturate(dot(n,view)),5);
    float3 reflection=texCUBElod(_ReflectionCube,float4(reflect(-view,n),1.8)).rgb;
    float depthTint=noise(p*.012)*.16+.84;
    float3 col=lerp(_Color.rgb*depthTint,reflection*.8,fres);col+=(noise(p*.33+t*.02)-.5)*.012*fade;
    float quay=step(-100,p.y)*step(p.y,790);
    float foam=(1-smoothstep(0,2,abs(p.x-_ShoreX)))*smoothstep(.48,.75,noise(float2(p.y*.4+t*.07,p.x*.5)))*.35*quay;
    foam+=i.contact*smoothstep(.32,.72,noise(p*.59+float2(t*.09,-t*.06)))*.72;
    col=lerp(col,float3(.42,.46,.45),foam);fixed4 result=fixed4(col,1);UNITY_APPLY_FOG(i.fogCoord,result);return result;
   }
   ENDCG
  }
 }
 FallBack Off
}
