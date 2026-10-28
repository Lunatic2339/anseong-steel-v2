using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace CoastalFactory.Editor {
public static class ShippingContainerAssetBuilder {
 const string Root="Assets/_Anseong/Art/Environment/Assets/ShippingContainer";
 static readonly List<GameObject> pieces=new List<GameObject>();
 static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;var parent=Path.GetDirectoryName(p).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(p));}
 static Material Mat(string name,Color color){var existing=AssetDatabase.LoadAssetAtPath<Material>(Root+"/"+name+".mat");if(existing)return existing;var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;m.SetFloat("_Smoothness",.08f);m.enableInstancing=true;AssetDatabase.CreateAsset(m,Root+"/"+name+".mat");return m;}
 static void Box(Transform parent,string name,Vector3 pos,Vector3 size,Material mat){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(g.GetComponent<Collider>());pieces.Add(g);}
 static Mesh Bake(Transform root,Material[] materials){var temps=new List<Mesh>();var final=new CombineInstance[materials.Length];for(int i=0;i<materials.Length;i++){var list=new List<CombineInstance>();foreach(var g in pieces)if(g.GetComponent<Renderer>().sharedMaterial==materials[i])list.Add(new CombineInstance{mesh=g.GetComponent<MeshFilter>().sharedMesh,transform=root.worldToLocalMatrix*g.transform.localToWorldMatrix});var part=new Mesh();part.CombineMeshes(list.ToArray(),true,true);temps.Add(part);final[i]=new CombineInstance{mesh=part,transform=Matrix4x4.identity};}var mesh=new Mesh{name="ShippingContainer"};mesh.CombineMeshes(final,false,true);mesh.RecalculateBounds();foreach(var m in temps)Object.DestroyImmediate(m);return mesh;}
 static void Beam(Transform root,string name,Vector3 a,Vector3 b,float width,Material material){Box(root,name,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),material);pieces[pieces.Count-1].transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
 [MenuItem("Anseong/Art/Build Shipping Container")]
 public static void Build(){Folder(Root);var paint=Mat("ContainerPaint",new Color(.18f,.36f,.40f));var steel=Mat("StructuralSteel",new Color(.24f,.28f,.29f));var dark=Mat("Windows_Cables",new Color(.045f,.065f,.08f));var trim=Mat("SafetyTrim",new Color(.75f,.72f,.59f));pieces.Clear();var root=new GameObject("ShippingContainer");var r=root.transform;
  Box(r,"Body",new Vector3(0,1.295f,0),new Vector3(2.35f,2.51f,6.0f),paint);
  for(int side=-1;side<=1;side+=2){
   for(int i=0;i<24;i++)Box(r,"SideCorrugation",new Vector3(side*1.19f,1.3f,-2.82f+i*.245f),new Vector3(.058f,2.25f,.08f),paint);
   Box(r,"BottomRail",new Vector3(side*1.17f,.10f,0),new Vector3(.10f,.20f,6.058f),steel);
   Box(r,"TopRail",new Vector3(side*1.17f,2.51f,0),new Vector3(.10f,.16f,6.058f),steel);
   for(int z=-1;z<=1;z+=2)Box(r,"CornerPost",new Vector3(side*1.16f,1.295f,z*2.969f),new Vector3(.118f,2.59f,.12f),steel);
  }
  for(int i=0;i<24;i++)Box(r,"RoofRib",new Vector3(0,2.56f,-2.82f+i*.245f),new Vector3(2.25f,.06f,.08f),paint);
  for(int side=-1;side<=1;side+=2){
   Box(r,"Door",new Vector3(side*.584f,1.3f,-3.018f),new Vector3(1.13f,2.31f,.025f),paint);
   for(int i=0;i<4;i++)Box(r,"DoorRib",new Vector3(side*.584f,.42f+i*.58f,-3.04f),new Vector3(1.02f,.07f,.04f),paint);
   Box(r,"LockRod",new Vector3(side*.72f,1.30f,-3.069f),new Vector3(.035f,2.15f,.035f),trim);
   Box(r,"Latch",new Vector3(side*.60f,1.0f,-3.09f),new Vector3(.28f,.045f,.06f),trim);
  }
  Box(r,"DoorSeam",new Vector3(0,1.3f,-3.041f),new Vector3(.025f,2.35f,.012f),dark);  var mats=new[]{paint,steel,dark,trim};var mesh=Bake(r,mats);Unwrapping.GenerateSecondaryUVSet(mesh);mesh.uv=mesh.uv2;ExportObj(mesh,mats);mesh.name="ShippingContainer";string path=Root+"/ShippingContainer.asset";var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(previous){EditorUtility.CopySerialized(mesh,previous);Object.DestroyImmediate(mesh);mesh=previous;}else AssetDatabase.CreateAsset(mesh,path);foreach(var g in pieces)Object.DestroyImmediate(g);pieces.Clear();root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterials=mats;var col=root.AddComponent<BoxCollider>();col.center=new Vector3(0,1.295f,0);col.size=new Vector3(2.438f,2.59f,6.058f);var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/ShippingContainer.prefab");Object.DestroyImmediate(root);
  Folder("Assets/_Anseong/Scenes/Sandbox");EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);PrefabUtility.InstantiatePrefab(prefab);var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="PreviewGround";ground.transform.position=new Vector3(0,-.2f,0);ground.transform.localScale=new Vector3(30,.4f,30);ground.GetComponent<Renderer>().sharedMaterial=steel;var camera=Camera.main;camera.transform.position=new Vector3(9,6,-11);camera.transform.LookAt(new Vector3(0,1.2f,0));camera.fieldOfView=43;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.27f,.37f,.46f);var sun=Object.FindFirstObjectByType<Light>();sun.intensity=2;sun.transform.rotation=Quaternion.Euler(40,-35,0);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.68f,.72f);EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/_Anseong/Scenes/Sandbox/Sandbox_ShippingContainer.unity");AssetDatabase.SaveAssets();int tris=mesh.triangles.Length/3;if(tris>3500||mesh.uv.Length!=mesh.vertexCount||mesh.bounds.max.y>2.61f||prefab.GetComponentsInChildren<Rigidbody>().Length!=0)throw new Exception("Container validation failed");Capture(camera);Debug.Log("CONTAINER_TEST_PASS triangles="+tris+" dimensions="+mesh.bounds.size+" sharedMeshes=1 materials=4 rigidbodies=0");
 }
 static void ExportObj(Mesh mesh,Material[] materials){var s=new System.Text.StringBuilder();var ci=System.Globalization.CultureInfo.InvariantCulture;foreach(var v in mesh.vertices)s.AppendLine("v "+v.x.ToString(ci)+" "+v.y.ToString(ci)+" "+v.z.ToString(ci));foreach(var uv in mesh.uv)s.AppendLine("vt "+uv.x.ToString(ci)+" "+uv.y.ToString(ci));foreach(var n in mesh.normals)s.AppendLine("vn "+n.x.ToString(ci)+" "+n.y.ToString(ci)+" "+n.z.ToString(ci));for(int sub=0;sub<mesh.subMeshCount;sub++){s.AppendLine("g "+materials[sub].name);s.AppendLine("usemtl "+materials[sub].name);var t=mesh.GetTriangles(sub);for(int i=0;i<t.Length;i+=3){s.Append("f");for(int j=0;j<3;j++){int k=t[i+j]+1;s.Append(" "+k+"/"+k+"/"+k);}s.AppendLine();}}File.WriteAllText(Root+"/ShippingContainer_Painter.obj",s.ToString());}
 static void Capture(Camera camera){var rt=new RenderTexture(1200,850,24);var old=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1200,850,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,850),0,0);image.Apply();File.WriteAllBytes("C:/Users/Admin/Documents/ChatGPT/안성제철/output/ShippingContainer_Preview.png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);}
}
}


