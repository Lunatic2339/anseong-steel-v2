using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace CoastalFactory.Editor {public static partial class LeftFactoryDistrictBuilder {
static Vector2 TreeXZ(Vector3 p)=>new Vector2(p.x,p.z);
static float TreeCross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
class TreeMask {
 readonly List<Vector2[]> tris=new List<Vector2[]>();
 public TreeMask(MeshFilter f,int slot){
 var mesh=f.sharedMesh;var v=mesh.vertices;var ix=mesh.GetTriangles(slot);
 for(int i=0;i<ix.Length;i+=3)tris.Add(new[]{TreeXZ(f.transform.TransformPoint(v[ix[i]])),TreeXZ(f.transform.TransformPoint(v[ix[i+1]])),TreeXZ(f.transform.TransformPoint(v[ix[i+2]]))});
 }
 public bool Has(Vector2 p){foreach(var t in tris){
 var a=TreeCross(t[1]-t[0],p-t[0]);var b=TreeCross(t[2]-t[1],p-t[1]);var c=TreeCross(t[0]-t[2],p-t[2]);
 if((a>0&&b>0&&c>0)||(a<0&&b<0&&c<0))return true;
 }return false;}
}
public static void PlantBattlefieldTrees(){
 EditorSceneManager.OpenScene(Scene);
 if(GameObject.Find("Battlefield_ScreeningLandscape"))throw new Exception("Already planted");
 var ground=GameObject.Find("Ground_ContinuousRoadBasedDesign").GetComponent<MeshFilter>();
 var grass=new TreeMask(ground,4);var walks=new TreeMask(ground,5);
 var roads=new TreeMask(GameObject.Find("Roads_CentrelineRebuilt").GetComponent<MeshFilter>(),0);
 var bounds=new List<Bounds>();var buildings=new List<Transform>();
 foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)){
 if(!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))continue;
 var p=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
 if(!p.Contains("/Environment/"))continue;
 var b=B(t.gameObject);if(b.size.x<.4f||b.size.z<.4f||b.size.x>300||b.size.z>300)continue;bounds.Add(b);
 if(b.size.y>6&&b.size.x>15&&b.size.z>15&&(p.Contains("Warehouse")||p.Contains("RollingMill")||p.Contains("SteelmakingHall")||p.Contains("UtilityPlant")||p.Contains("PumpStation")))buildings.Add(t);
 }
 var prefabs=new GameObject[3];for(int i=0;i<3;i++)prefabs[i]=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Anseong/Art/Environment/Assets/ScreeningTrees/ScreeningTree_"+(char)('A'+i)+".prefab");
 var root=new GameObject("Battlefield_ScreeningLandscape");
 var rows=new GameObject("Building_Perimeter_Rows");rows.transform.SetParent(root.transform);
 var groves=new GameObject("Roadside_Green_Groves");groves.transform.SetParent(root.transform);
 var rng=new System.Random(9037);var planted=new List<Vector2>();
 bool Near(Vector2 p)=>p.x>=-275&&p.x<=247&&p.y>=-350&&p.y<=385&&!(Mathf.Abs(p.x)<68&&p.y>-240&&p.y<230);
 bool Place(Vector2 p,Transform parent,bool grove){
 if(!Near(p))return false;
 foreach(var b in bounds)if(p.x>b.min.x-1.05f&&p.x<b.max.x+1.05f&&p.y>b.min.z-1.05f&&p.y<b.max.z+1.05f)return false;
 foreach(var q in planted)if((p-q).sqrMagnitude<(grove?2.8f:3.4f)*(grove?2.8f:3.4f))return false;
 foreach(var d in new[]{Vector2.zero,new Vector2(.85f,0),new Vector2(-.85f,0),new Vector2(0,.85f),new Vector2(0,-.85f)})
 if(roads.Has(p+d)||walks.Has(p+d)||(grove&&!grass.Has(p+d)))return false;
 var g=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[rng.Next(3)],parent);
 g.transform.SetPositionAndRotation(new Vector3(p.x,.03f,p.y),Quaternion.Euler(0,rng.Next(360),0));
 foreach(var col in g.GetComponentsInChildren<Collider>())col.enabled=false;
 planted.Add(p);return true;
 }
 int rowCount=0,buildingCount=0;
 foreach(var t in buildings){
 var b=B(t.gameObject);var center=TreeXZ(b.center);if(!Near(center))continue;
 var local=new Bounds();bool first=true;
 foreach(var renderer in t.GetComponentsInChildren<MeshRenderer>()){
 var rb=renderer.localBounds;
 for(int j=0;j<8;j++){
 var q=t.InverseTransformPoint(renderer.transform.TransformPoint(rb.center+Vector3.Scale(rb.extents,new Vector3((j&1)==0?-1:1,(j&2)==0?-1:1,(j&4)==0?-1:1))));
 if(first){local=new Bounds(q,Vector3.zero);first=false;}else local.Encapsulate(q);
 }}
 if(first)continue;
 var scale=t.lossyScale;
 float x0=local.min.x-2.8f/Mathf.Abs(scale.x),x1=local.max.x+2.8f/Mathf.Abs(scale.x),z0=local.min.z-2.8f/Mathf.Abs(scale.z),z1=local.max.z+2.8f/Mathf.Abs(scale.z);
 var corners=new[]{new Vector3(x0,0,z0),new Vector3(x1,0,z0),new Vector3(x1,0,z1),new Vector3(x0,0,z1)};
 var front=t.InverseTransformPoint(new Vector3(0,0,0))-local.center;
 int entrance=Mathf.Abs(front.x)>Mathf.Abs(front.z)?(front.x>0?1:3):(front.z>0?2:0);
 int added=0;
 for(int edge=0;edge<4;edge++){
 var a=TreeXZ(t.TransformPoint(corners[edge]));var e=TreeXZ(t.TransformPoint(corners[(edge+1)%4]));
 int count=Mathf.Max(1,Mathf.FloorToInt(Vector2.Distance(a,e)/4.5f));
 for(int i=0;i<count;i++){float u=(i+.5f)/count;if(edge==entrance&&u>.19f&&u<.81f)continue;if(Place(Vector2.Lerp(a,e,u),rows.transform,false))added++;}
 }
 if(added>0){rowCount+=added;buildingCount++;}
 }
 for(int x=-275;x<=247;x+=4)for(int z=-350;z<=385;z+=4){
 if(groves.transform.childCount>=240)break;
 var p=new Vector2(x+(float)rng.NextDouble()*2,z+(float)rng.NextDouble()*2);
 if(grass.Has(p))Place(p,groves.transform,true);
 }
 EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Scene);
 Debug.Log("BATTLEFIELD_TREES_PASS buildings="+buildingCount+" rows="+rowCount+" groves="+groves.transform.childCount);
}
}}

