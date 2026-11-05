using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CoastalFactory.Editor {public static partial class LeftFactoryDistrictBuilder {
struct LPeak {public float x,z,h,wx,wz;public bool side;}
static LPeak[] LPeaks(){
var random=new System.Random(511057);var peaks=new LPeak[15];int index=0;
for(int row=0;row<2;row++){float x=row==0?125:90;for(int i=0;i<5;i++){
peaks[index++]=new LPeak{x=x,z=(row==0?1460:1910)+random.Next(-95,96),h=random.Next(row==0?108:133,row==0?149:168),wx=random.Next(325,416),wz=random.Next(405,516),side=false};
x-=random.Next(310,531);
}}
float z=-220+random.Next(0,61);
for(int i=0;i<5;i++){peaks[index++]=new LPeak{x=-1500+random.Next(-120,81),z=z,h=random.Next(110,161),wx=random.Next(310,381),wz=random.Next(350,481),side=true};z+=random.Next(245,371);}
peaks[0].h=0;peaks[5].h=0;return peaks;
}
static float LMountainHeight(float x,float z,LPeak[] peaks){
float rearGate=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1080,1170,z));
float rear=66*Mathf.SmoothStep(0,1,Mathf.InverseLerp(1100,1710,z));
float sideGate=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-1600,-1020,x)))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(-850,-400,z));
float rearBump=0,sideBump=0;
foreach(var p in peaks){float dx=(x-p.x)/p.wx,dz=(z-p.z)/p.wz;float q=Mathf.Clamp01(1-Mathf.Sqrt(dx*dx+dz*dz));float bump=q*q*(3-2*q)*p.h;if(p.side)sideBump=Mathf.Max(sideBump,bump);else rearBump=Mathf.Max(rearBump,bump);}
float value=Mathf.Max(rear+rearBump*rearGate,88*sideGate+sideBump*sideGate);
float bank=34*Mathf.SmoothStep(0,1,Mathf.InverseLerp(1100,1500,z))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1800,2400,z)));value=Mathf.Lerp(value,bank,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-180,120,x)));
float taper=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(175,250,x));return value*taper-.08f;
}
public static void ArrangeLMountainLayout(){
EditorSceneManager.OpenScene(Scene);var peaks=LPeaks();const string folder="Assets/_Anseong/Art/Environment/CompactMountainMap";
var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/CompactNearMountainTerrain.asset");var vertices=mesh.vertices;
for(int i=0;i<vertices.Length;i++){var p=vertices[i];p.y=LMountainHeight(p.x,p.z,peaks);vertices[i]=p;}
int rays=0;foreach(var eye in new[]{new Vector3(0,65,-120),new Vector3(0,65,140)})for(float x=-3000;x<=225;x+=25){var edge=new Vector3(x,LMountainHeight(x,CompactRear,peaks),CompactRear);bool hidden=false;for(float t=.02f;t<.99f;t+=.01f){var p=Vector3.Lerp(eye,edge,t);if(LMountainHeight(p.x,p.z,peaks)>p.y+2){hidden=true;break;}}if(!hidden)throw new Exception("Rear edge exposed at X="+x);rays++;}
var factory=GameObject.Find("Factory_HighlightedLZone");int buildings=0;foreach(var t in factory.GetComponentsInChildren<Transform>())if(PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)){var b=B(t.gameObject);foreach(float x in new[]{b.min.x,b.center.x,b.max.x})foreach(float z in new[]{b.min.z,b.center.z,b.max.z})if(LMountainHeight(x,z,peaks)>.5f)throw new Exception("Hill enters factory:"+t.name);buildings++;}
mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();
string report="# L-shaped mountain layout\n\n13 peaks remain:8 rear and5 left. The two overlapping seaward-end peaks were removed; a low bank keeps the cropped edge hidden without the tall silhouette. Reproducible integer random generator System.Random seed511057; rear gaps310–530m, side gaps245–370m, independent position/height/width jitter. Existing cropped meshXZ and rearZ2400 boundary unchanged. Current foliage material reused, no new textures or runtime components.\n\n";
for(int i=0;i<peaks.Length;i++){var p=peaks[i];report+=$"- Peak{i+1}: {(p.side?"left":"rear")} X{p.x} Z{p.z}; height{p.h}, widths{p.wx}/{p.wz}\n";}
report+=$"\nValidation: {rays} rear-edge rays remain hidden from both65m eye proxies; {buildings} factory prefab footprints remain flat.\n";
File.WriteAllText(folder+"/LMountainLayout.md",report);AssetDatabase.Refresh();Debug.Log("L_MOUNTAINS_PASS total=13 rear=8 left=5 occludedRays="+rays+" factoryClear="+buildings);
}
}}
