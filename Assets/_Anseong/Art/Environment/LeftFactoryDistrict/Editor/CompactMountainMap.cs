using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace CoastalFactory.Editor {public static partial class LeftFactoryDistrictBuilder {
const float CompactRear=2400;
static float CompactHill(float x,float z){
if(z<=1080)return -.08f;
float y=66*Mathf.SmoothStep(0,1,Mathf.InverseLerp(1100,1710,z));
float[] cx={-3000,-2510,-2020,-1540,-1090,-650,-235,125,-2750,-2220,-1710,-1220,-760,-310,120};
float[] cz={1450,1510,1400,1490,1410,1500,1420,1500,1900,1870,1970,1840,1990,1870,2000};
float[] h={115,153,126,142,121,137,112,105,135,155,145,162,140,157,148};
float bump=0;
for(int i=0;i<cx.Length;i++){
float dx=(x-cx[i])/(i<8?350:415),dz=(z-cz[i])/(i<8?420:510);
float q=Mathf.Clamp01(1-Mathf.Sqrt(dx*dx+dz*dz));
bump=Mathf.Max(bump,q*q*(3-2*q)*h[i]);
}
float taper=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(175,250,x));
return (y+bump)*taper-.08f;
}
struct ClipVertex {public Vector3 p,n;public Vector2 uv;public Color color;}
static Mesh ClipRearMesh(MeshFilter filter){
var src=filter.sharedMesh;var pv=src.vertices;var pn=src.normals;var pu=src.uv;var pc=src.colors;
var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var slots=new List<int>[src.subMeshCount];
ClipVertex Vertex(int index){return new ClipVertex{p=pv[index],n=pn.Length==pv.Length?pn[index]:Vector3.up,uv=pu.Length==pv.Length?pu[index]:Vector2.zero,color=pc.Length==pv.Length?pc[index]:Color.white};}
foreach(int sub in System.Linq.Enumerable.Range(0,src.subMeshCount)){
slots[sub]=new List<int>();var indices=src.GetTriangles(sub);
for(int j=0;j<indices.Length;j+=3){var original=new[]{Vertex(indices[j]),Vertex(indices[j+1]),Vertex(indices[j+2])};var polygon=new List<ClipVertex>();
for(int k=0;k<3;k++){var a=original[k];var b=original[(k+1)%3];float az=filter.transform.TransformPoint(a.p).z,bz=filter.transform.TransformPoint(b.p).z;bool inside=az<=CompactRear;
if(inside)polygon.Add(a);
if(inside!=(bz<=CompactRear)){float f=(CompactRear-az)/(bz-az);polygon.Add(new ClipVertex{p=Vector3.Lerp(a.p,b.p,f),n=Vector3.Lerp(a.n,b.n,f).normalized,uv=Vector2.Lerp(a.uv,b.uv,f),color=Color.Lerp(a.color,b.color,f)});}}
for(int k=1;k+1<polygon.Count;k++)foreach(int q in new[]{0,k,k+1}){var v=polygon[q];slots[sub].Add(vertices.Count);vertices.Add(v.p);normals.Add(v.n);uv.Add(v.uv);colors.Add(v.color);}
}}
var result=new Mesh{name=src.name+"_Cropped",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};result.SetVertices(vertices);result.SetNormals(normals);if(pu.Length==pv.Length)result.SetUVs(0,uv);if(pc.Length==pv.Length)result.SetColors(colors);result.subMeshCount=slots.Length;for(int i=0;i<slots.Length;i++)result.SetTriangles(slots[i],i);result.RecalculateBounds();return result;
}
public static void BuildCompactMountainMap(){
EditorSceneManager.OpenScene(Scene);const string folder="Assets/_Anseong/Art/Environment/CompactMountainMap";Folder(folder);
var mountain=GameObject.Find("ConnectedCoastalHinterland");var material=mountain.GetComponent<MeshRenderer>().sharedMaterial;
int nx=220,nz=220;var vertices=new Vector3[(nx+1)*(nz+1)];var triangles=new List<int>();
for(int iz=0;iz<=nz;iz++)for(int ix=0;ix<=nx;ix++){float x=Mathf.Lerp(-3200,250,(float)ix/nx),z=Mathf.Lerp(-3000,CompactRear,(float)iz/nz);vertices[iz*(nx+1)+ix]=new Vector3(x,CompactHill(x,z),z);if(iz<nz&&ix<nx){int a=iz*(nx+1)+ix,c=a+nx+1;triangles.AddRange(new[]{a,c,a+1,a+1,c,c+1});}}
var mesh=new Mesh{name="CompactNearMountainTerrain",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,folder+"/CompactNearMountainTerrain.asset");mountain.GetComponent<MeshFilter>().sharedMesh=mesh;mountain.GetComponent<MeshRenderer>().sharedMaterial=material;
// Land and beach surfaces stop at the marked rear line. Ocean remains decorative horizon geometry.
int clipped=0;foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)){
if(filter.gameObject==mountain||!filter.sharedMesh||filter.name=="Ocean")continue;
var renderer=filter.GetComponent<MeshRenderer>();if(!renderer||renderer.bounds.max.z<=CompactRear+.01f)continue;
if(renderer.bounds.min.z>CompactRear)throw new Exception("Unexpected rear object:"+filter.name);
var crop=ClipRearMesh(filter);AssetDatabase.CreateAsset(crop,folder+"/Crop_"+clipped+".asset");filter.sharedMesh=crop;clipped++;
}
int colliders=0;var land=GameObject.Find("CoastalLand");foreach(var box in land.GetComponentsInChildren<BoxCollider>()){
if(box.bounds.max.z<=CompactRear)continue;
if(box.bounds.min.z>=CompactRear){box.enabled=false;continue;}
var min=box.center.z-box.size.z*.5f;var end=box.transform.InverseTransformPoint(new Vector3(box.bounds.center.x,box.bounds.center.y,CompactRear)).z;
var size=box.size;size.z=end-min;var center=box.center;center.z=(end+min)*.5f;box.size=size;box.center=center;colliders++;
}
// Conservative line-of-sight test: from65m high at both robot positions to the entire rear terrain edge.
int checkedRays=0;foreach(var eye in new[]{new Vector3(0,65,-120),new Vector3(0,65,140)})for(float x=-3000;x<=225;x+=25){
var edge=new Vector3(x,CompactHill(x,CompactRear),CompactRear);bool hidden=false;
for(float t=.02f;t<.99f;t+=.01f){var q=Vector3.Lerp(eye,edge,t);if(CompactHill(q.x,q.z)>q.y+2){hidden=true;break;}}
if(!hidden)throw new Exception("Rear boundary visible at X="+x+" eye="+eye);checkedRays++;
}
File.WriteAllText(folder+"/LayoutReport.md","# Compact mountain map\n\nPink-line approximation: foothills startZ1080–1150, just behind the factory rear roadZ1030.15 rounded peaks in two staggered rows; closer peaks nearZ1400–1510, rear peaksZ1840–2000. Terrain rear cropped atZ2400, matching the red-line composition. New terrain X-3200–250,Z-3000–2400; prior rear terrain reachedZ13000. Industrial land, beach and land collision also clipped atZ2400. Ocean retained as non-colliding visual horizon background; combat yard and buildings unchanged. Existing June-green shader/material reused; no additional bitmap textures.\n\nOcclusion verification: "+checkedRays+" rays from both65m robot-eye proxies to rear edge X-3000..225, all blocked by terrain with2m margin. This is a static composition check, not headset validation or an occlusion-culling benchmark. User annotation interpreted from screenshot, not surveyed coordinates.\nMeshes clipped="+clipped+"; land box colliders shortened="+colliders+".\n");
AssetDatabase.SaveAssets();AssetDatabase.Refresh();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Scene);Debug.Log("COMPACT_MOUNTAIN_MAP_PASS peaks=15 rearZ="+CompactRear+" clippedMeshes="+clipped+" hiddenRearEdgeRays="+checkedRays);
}
public static void PreviewCompactMountainMap(){EditorSceneManager.OpenScene(Scene);var go=new GameObject("CompactMapPreview");var camera=go.AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.fieldOfView=60;camera.transform.position=new Vector3(0,65,-120);camera.transform.LookAt(new Vector3(0,75,1600));Capture(camera,"CompactMap_Player65m");camera.transform.position=new Vector3(0,65,140);camera.transform.LookAt(new Vector3(0,75,1600));Capture(camera,"CompactMap_Boss65m");camera.transform.position=new Vector3(-300,1100,-350);camera.transform.LookAt(new Vector3(-400,0,1200));Capture(camera,"CompactMap_Aerial");Object.DestroyImmediate(go);Debug.Log("COMPACT_MAP_PREVIEWS_PASS");}}}
