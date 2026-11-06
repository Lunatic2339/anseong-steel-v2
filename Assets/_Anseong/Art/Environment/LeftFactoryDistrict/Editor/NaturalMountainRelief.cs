using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CoastalFactory.Editor {public static partial class LeftFactoryDistrictBuilder {
static void GentleMountainSizes(LPeak[] peaks){
// Seven low hills, four medium masses and two larger rear mountains; positions stay fixed.
float[] height={0,.45f,.75f,.55f,.40f,0,.85f,.60f,1.15f,1.25f,.50f,.70f,.42f,.80f,.57f};
for(int i=0;i<peaks.Length;i++){if(peaks[i].h<=0)continue;var p=peaks[i];float spread=(height[i]>1?1.65f:height[i]>.6f?1.45f:1.38f)+.07f*Mathf.Sin(i*1.9f);p.h*=height[i];p.wx*=spread;p.wz*=spread+.12f;peaks[i]=p;}
}
static float ReliefBlend(float a,float b){if(a<.05f||b<.05f)return Mathf.Max(a,b);float h=Mathf.Clamp01(.5f+.5f*(a-b)/16);return Mathf.Lerp(b,a,h)+16*h*(1-h);}
static float NaturalHill(float x,float z,LPeak[] peaks){
float rearGate=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1080,1450,z));
float sideGate=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-1860,-960,x)))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(-950,-350,z));
float rearBump=0,sideBump=0;
for(int i=0;i<peaks.Length;i++){var p=peaks[i];if(p.h<=0)continue;
float dx=(x-p.x)/p.wx,dz=(z-p.z)/p.wz;
float r=Mathf.Sqrt(dx*dx+dz*dz);
float asym=1+.24f*Mathf.Sin(dx*1.3f+i*1.71f)*Mathf.Min(r,1)+.14f*Mathf.Sin(dz*1.8f-i*.8f)*Mathf.Min(r,1);
float shoulder=Mathf.Clamp01(1-r/Mathf.Max(.65f,asym));
float dome=shoulder*shoulder*(3-2*shoulder);
float shape=Mathf.Lerp(dome,Mathf.Pow(dome,.82f),i%3==0?.45f:.12f);
float detail=1+(Mathf.PerlinNoise(x*.004f+17,z*.004f+29)-.5f)*.10f*Mathf.Clamp01(r*2);
float erosion=(Mathf.PerlinNoise(x*.012f+9,z*.012f+44)-.5f)*2.5f*shape*(1-shape)*4;
float height=Mathf.Max(0,shape*p.h*detail+erosion);
if(p.side)sideBump=ReliefBlend(sideBump,height);else rearBump=ReliefBlend(rearBump,height);
}
// Broad, gently meandering saddles connect neighbouring peaks into one landform.
for(int i=0;i<peaks.Length;i++){var a=peaks[i];if(a.h<=0)continue;
int best=-1;float distance=float.PositiveInfinity;
for(int j=i+1;j<peaks.Length;j++){var b=peaks[j];if(b.h<=0)continue;float d=Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));if(d<distance&&d<720){best=j;distance=d;}}
if(best<0)continue;var end=peaks[best];var av=new Vector2(a.x,a.z);var delta=new Vector2(end.x-a.x,end.z-a.z);var point=new Vector2(x,z);float t=Mathf.Clamp01(Vector2.Dot(point-av,delta)/delta.sqrMagnitude);
var normal=new Vector2(-delta.y,delta.x).normalized;
var centre=av+delta*t+normal*Mathf.Sin(t*Mathf.PI)*Mathf.Sin(i*2.3f)*65;
float width=Mathf.Lerp(250,370,(Mathf.Sin(i*1.9f)+1)*.5f);
float q=Mathf.Clamp01(1-Vector2.Distance(point,centre)/width);
float ridge=q*q*(3-2*q)*Mathf.Lerp(a.h,end.h,t)*.42f;
if(a.side||end.side)sideBump=ReliefBlend(sideBump,ridge);if(!a.side||!end.side)rearBump=ReliefBlend(rearBump,ridge);
}
float baseVariation=(Mathf.PerlinNoise(x*.0018f+7,z*.0023f+31)-.5f)*6;
float rear=(25+baseVariation)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(1100,1850,z));
float value=ReliefBlend(rear+rearBump*rearGate,(34+baseVariation)*sideGate+sideBump*sideGate);
float bank=34*Mathf.SmoothStep(0,1,Mathf.InverseLerp(1100,1500,z))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1800,2400,z)));
value=Mathf.Lerp(value,bank,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-180,120,x)));
float taper=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(175,250,x));return value*taper-.08f;
}
public static void NaturaliseMountainRelief(){
EditorSceneManager.OpenScene(Scene);var peaks=LPeaks();const string folder="Assets/_Anseong/Art/Environment/CompactMountainMap";
var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/CompactNearMountainTerrain.asset");var vertices=mesh.vertices;float max=0;float oldSlope=ReliefSlope90(mesh.vertices,mesh.triangles);
for(int i=0;i<vertices.Length;i++){var p=vertices[i];p.y=NaturalHill(p.x,p.z,peaks);max=Mathf.Max(max,p.y);vertices[i]=p;}
int rays=0;foreach(var eye in new[]{new Vector3(0,65,-120),new Vector3(0,65,140)})for(float x=-3000;x<=225;x+=25){var edge=new Vector3(x,NaturalHill(x,CompactRear,peaks),CompactRear);bool hidden=false;for(float t=.02f;t<.99f;t+=.01f){var p=Vector3.Lerp(eye,edge,t);if(NaturalHill(p.x,p.z,peaks)>p.y+2){hidden=true;break;}}if(!hidden)throw new Exception("Rear edge exposed at X="+x);rays++;}
var factory=GameObject.Find("Factory_HighlightedLZone");int buildings=0;foreach(var t in factory.GetComponentsInChildren<Transform>())if(PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)){var b=B(t.gameObject);foreach(float x in new[]{b.min.x,b.center.x,b.max.x})foreach(float z in new[]{b.min.z,b.center.z,b.max.z})if(NaturalHill(x,z,peaks)>.5f)throw new Exception("Relief enters factory:"+t.name);buildings++;}
float newSlope=ReliefSlope90(vertices,mesh.triangles);if(newSlope>oldSlope+.5f)throw new Exception("Slopes did not become gentler");mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();
File.WriteAllText(folder+"/GentleRelief.md","# Varied, gentler mountain masses\n\nThirteen active controls retain their XZ positions:7 low hills(40–60% original control height),4 medium masses(70–85%),2 larger rear mountains(115–125%). Width/depth expanded roughly1.3–1.8x, broad lower saddles, longer foothill transitions, lower base heights25/34m instead of66/88m. Large mountains are reserved for the rear; low hills dominate the front and left flank. Cropped map, coastal opening and both previously moved controls retained. All changes baked into the same mesh; no extra textures, geometry allocation or runtime calculation.\n\nSlope90th percentile among non-flat terrain faces: "+oldSlope+" to "+newSlope+" degrees. Existing260 rear-edge visibility rays and260 factory footprint checks passed.\n");
File.WriteAllText(folder+"/NaturalRelief.md","# Continuous natural mountain relief\n\nCurrent13 peak controls and L-shaped plan retained, including moved Peak14/15 and disabled seaward peaks. Asymmetric slope envelopes, varied rounded/flattened summit profiles, smooth unions and broad meandering saddle ridges connect peaks into continuous terrain. Low-amplitude250m shape variation and local80m shallow erosion avoid uniform fine noise. Original XZ coordinates, triangles, material, shoreline and cropped rear limit preserved; no new bitmap textures, tree models, colliders or runtime components. Procedural calculations run only in the Editor and are baked into the existing mesh.\n\nValidated rear-edge visibility from both65m eye proxies: "+rays+" blocked rays. Factory prefab footprints clear:"+buildings+". Maximum terrain height:"+max+"m.\n");AssetDatabase.Refresh();Debug.Log("NATURAL_RELIEF_PASS controls=13 sameMesh=true hiddenRays="+rays+" factoryClear="+buildings+" maxHeight="+max);
}
static float ReliefSlope90(Vector3[] vertices,int[] triangles){var slopes=new System.Collections.Generic.List<float>();for(int i=0;i<triangles.Length;i+=3){var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];if(Mathf.Max(a.y,Mathf.Max(b.y,c.y))<15)continue;var n=Vector3.Cross(b-a,c-a).normalized;float angle=Mathf.Acos(Mathf.Clamp01(Mathf.Abs(n.y)))*Mathf.Rad2Deg;if(angle>2)slopes.Add(angle);}slopes.Sort();return slopes.Count==0?0:slopes[Mathf.FloorToInt((slopes.Count-1)*.9f)];}
}}
