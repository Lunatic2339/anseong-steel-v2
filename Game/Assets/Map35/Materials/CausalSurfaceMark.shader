Shader "Anseong/CausalSurfaceMark" {
 Properties { _MainTex("UV carrier",2D)="white" {} _Color("Deposit color",Color)=(.14,.11,.08,.6) _Kind("0 soot 1 runoff 2 contact",Float)=0 _Seed("Variation",Float)=0 }
 SubShader {
  Tags {"Queue"="Transparent-8" "RenderType"="Transparent"}
  ZWrite Off Offset -1,-1
  CGPROGRAM
  #pragma surface surf Standard alpha:fade
  #pragma target 3.0
  fixed4 _Color;float _Kind,_Seed;
  struct Input {float2 uv_MainTex;};
  sampler2D _MainTex;
  float markHash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float markNoise(float2 p){float2 q=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(markHash(q),markHash(q+float2(1,0)),f.x),lerp(markHash(q+float2(0,1)),markHash(q+1),f.x),f.y);}
  void surf(Input IN,inout SurfaceOutputStandard o){
   float2 uv=IN.uv_MainTex,p=uv*2-1;
   float n=markNoise(uv*6+_Seed)*.68+markNoise(uv*21+_Seed)*.32;
   float envelope=1-smoothstep(.48+n*.15,.94,length(p));
   float alpha=envelope*smoothstep(.18,.7,n);
   if(_Kind>.5&&_Kind<1.5){
    // Starts directly below a fastener/seam; narrows and breaks as the water runs down.
    float down=1-uv.y,drift=(markNoise(float2(down*5,_Seed))-.5)*.16;
    float width=lerp(.26,.06,down);
    alpha=(1-smoothstep(width*.45,width,abs(p.x+drift)))*(1-smoothstep(.45,1,down))*(.4+.6*n);
   }
   if(_Kind>1.5)alpha=(1-smoothstep(.55,.98,max(abs(p.x),abs(p.y))))*smoothstep(.25,.66,n);
   o.Albedo=_Color.rgb*(.72+.28*n);o.Metallic=0;o.Smoothness=_Kind>2.5?.58:.04;o.Occlusion=1;o.Alpha=alpha*_Color.a;
  }
  ENDCG
 }
 FallBack Off
}
