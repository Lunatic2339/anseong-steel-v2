using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace CoastalFactory.Editor {
public static class UtilityPlantAssetBuilder {
 const string Root="Assets/_Anseong/Art/Environment/Assets/UtilityPlant";
 static readonly List<GameObject> pieces=new List<GameObject>();
 static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;var parent=Path.GetDirectoryName(p).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(p));}
 static Material Mat(string name,Color color){var existing=AssetDatabase.LoadAssetAtPath<Material>(Root+"/"+name+".mat");if(existing)return existing;var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;m.SetFloat("_Smoothness",.08f);m.enableInstancing=true;AssetDatabase.CreateAsset(m,Root+"/"+name+".mat");return m;}
 static void Box(Transform parent,string name,Vector3 pos,Vector3 size,Material mat){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(g.GetComponent<Collider>());pieces.Add(g);}
 static Mesh Bake(Transform root,Material[] materials){var temps=new List<Mesh>();var final=new CombineInstance[materials.Length];for(int i=0;i<materials.Length;i++){var list=new List<CombineInstance>();foreach(var g in pieces)if(g.GetComponent<Renderer>().sharedMaterial==materials[i])list.Add(new CombineInstance{mesh=g.GetComponent<MeshFilter>().sharedMesh,transform=root.worldToLocalMatrix*g.transform.localToWorldMatrix});var part=new Mesh();part.CombineMeshes(list.ToArray(),true,true);temps.Add(part);final[i]=new CombineInstance{mesh=part,transform=Matrix4x4.identity};}var mesh=new Mesh{name="UtilityPlant"};mesh.CombineMeshes(final,false,true);mesh.RecalculateBounds();foreach(var m in temps)Object.DestroyImmediate(m);return mesh;}
 static void Beam(Transform root,string name,Vector3 a,Vector3 b,float width,Material material){Box(root,name,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),material);pieces[pieces.Count-1].transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
 [MenuItem("Anseong/Art/Build Utility Plant")]
 public static void Build(){Folder(Root);var paint=Mat("WhiteCladding",new Color(.79f,.82f,.81f));var steel=Mat("StructuralSteel",new Color(.24f,.28f,.29f));var dark=Mat("Windows_Cables",new Color(.045f,.065f,.08f));var trim=Mat("SafetyTrim",new Color(.61f,.66f,.66f));pieces.Clear();var root=new GameObject("UtilityPlant");var r=root.transform;
  Box(r,"MainHall",new Vector3(0,11,0),new Vector3(52,22,26),paint);
  Box(r,"LowerServiceWing",new Vector3(-6,5.5f,-17),new Vector3(42,11,10),paint);
  Box(r,"Roof",new Vector3(0,22.25f,0),new Vector3(53,.5f,27),trim);
  for(int i=0;i<14;i++){float x=-24+i*3.7f;Box(r,"FacadeSeam",new Vector3(x,11,-13.03f),new Vector3(.06f,21,.08f),trim);Box(r,"HighWindow",new Vector3(x,18.5f,-13.08f),new Vector3(2.8f,2.1f,.12f),dark);Box(r,"RearWindow",new Vector3(x,18.5f,13.08f),new Vector3(2.8f,2.1f,.12f),dark);}
  for(int side=-1;side<=1;side+=2){Box(r,"FacadeBand",new Vector3(0,15,side*13.08f),new Vector3(52,.4f,.14f),steel);for(int i=0;i<8;i++)Box(r,"EndLouvre",new Vector3(side*26.08f,7+i*.6f,0),new Vector3(.16f,.26f,11),steel);}
  for(int i=0;i<3;i++){float x=-19+i*13;Box(r,"ServiceDoor",new Vector3(x,2,-22.07f),new Vector3(2.2f,4,.15f),steel);Box(r,"DoorCanopy",new Vector3(x,4.3f,-22.6f),new Vector3(3.5f,.18f,1.5f),trim);Box(r,"RoofVent",new Vector3(x,23.4f,0),new Vector3(5,2.3f,3),steel);Box(r,"VentCap",new Vector3(x,24.7f,0),new Vector3(5.6f,.3f,3.6f),trim);}
  for(int t=0;t<2;t++){float x=-16+t*26;float z=18;Box(r,"TowerFoundation",new Vector3(x,.5f,z),new Vector3(10,1,10),trim);for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2){Beam(r,"TowerLeg",new Vector3(x+sx*4.2f,1,z+sz*4.2f),new Vector3(x+sx*2.1f,62,z+sz*2.1f),.40f,paint);}for(int level=0;level<10;level++){float y=1+level*6;float w=Mathf.Lerp(4.2f,2.1f,level/10f);float wn=Mathf.Lerp(4.2f,2.1f,(level+1)/10f);for(int side=-1;side<=1;side+=2){Beam(r,"TowerBrace",new Vector3(x-w,y,z+side*w),new Vector3(x+wn,y+6,z+side*wn),.21f,paint);Beam(r,"TowerBrace",new Vector3(x+w,y,z+side*w),new Vector3(x-wn,y+6,z+side*wn),.21f,paint);Beam(r,"TowerBrace",new Vector3(x+side*w,y,z-w),new Vector3(x+side*wn,y+6,z+wn),.21f,paint);Beam(r,"TowerRing",new Vector3(x-w,y,z+side*w),new Vector3(x+w,y,z+side*w),.26f,trim);}}
   Box(r,"TowerDuct",new Vector3(x,18,z),new Vector3(1.6f,36,1.6f),steel);Beam(r,"ConnectingPipe",new Vector3(x,8,10),new Vector3(x,8,18),.7f,steel);}  Details(r,paint,steel,dark,trim);var mats=new[]{paint,steel,dark,trim};var mesh=Bake(r,mats);Unwrapping.GenerateSecondaryUVSet(mesh);mesh.uv=mesh.uv2;ExportObj(mesh,mats);AssetDatabase.Refresh();mesh.name="UtilityPlant";string path=Root+"/UtilityPlant.asset";var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(previous){EditorUtility.CopySerialized(mesh,previous);Object.DestroyImmediate(mesh);mesh=previous;mesh.UploadMeshData(false);}else AssetDatabase.CreateAsset(mesh,path);foreach(var g in pieces)Object.DestroyImmediate(g);pieces.Clear();root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterials=mats;var col=root.AddComponent<BoxCollider>();col.center=new Vector3(0,11,0);col.size=new Vector3(52,22,26);var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/UtilityPlant.prefab");Object.DestroyImmediate(root);
  Folder("Assets/_Anseong/Scenes/Sandbox");EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);PrefabUtility.InstantiatePrefab(prefab);var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="PreviewGround";ground.transform.position=new Vector3(0,-.2f,0);ground.transform.localScale=new Vector3(150,.4f,150);ground.GetComponent<Renderer>().sharedMaterial=steel;var camera=Camera.main;camera.transform.position=new Vector3(100,67,-120);camera.transform.LookAt(new Vector3(0,25,0));camera.fieldOfView=43;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.27f,.37f,.46f);var sun=Object.FindFirstObjectByType<Light>();sun.intensity=2;sun.transform.rotation=Quaternion.Euler(40,-35,0);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.68f,.72f);EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/_Anseong/Scenes/Sandbox/Sandbox_UtilityPlant.unity");AssetDatabase.SaveAssets();int tris=mesh.triangles.Length/3;if(tris>13000||mesh.uv.Length!=mesh.vertexCount||mesh.bounds.max.y>63f||prefab.GetComponentsInChildren<Rigidbody>().Length!=0)throw new Exception("Container validation failed");Capture(camera);Debug.Log("UTILITY_PLANT_TEST_PASS triangles="+tris+" dimensions="+mesh.bounds.size+" sharedMeshes=1 materials=4 rigidbodies=0");
 }
 static void Details(Transform r,Material paint,Material steel,Material dark,Material trim){
  for(int t=0;t<2;t++){float x=-16+t*26;float z=18;
   Box(r,"InternalStack",new Vector3(x,30,z),new Vector3(2.2f,60,2.2f),steel);
   for(int level=1;level<=10;level++){float y=1+level*6;float w=Mathf.Lerp(4.2f,2.1f,level/10f);for(int side=-1;side<=1;side+=2){Beam(r,"TransverseRing",new Vector3(x+side*w,y,z-w),new Vector3(x+side*w,y,z+w),.26f,trim);for(int sz=-1;sz<=1;sz+=2){Box(r,"JointPlate",new Vector3(x+side*w,y,z+sz*w),new Vector3(.70f,.85f,.5f),steel);}}
    if(level%2==0){Box(r,"ServicePlatform",new Vector3(x,y,z),new Vector3(w*2+1,.18f,w*2+1),steel);for(int side=-1;side<=1;side+=2){Beam(r,"PlatformRailing",new Vector3(x-w-.4f,y+1.1f,z+side*(w+.4f)),new Vector3(x+w+.4f,y+1.1f,z+side*(w+.4f)),.07f,paint);Beam(r,"PlatformSideRail",new Vector3(x+side*(w+.4f),y+1.1f,z-w-.4f),new Vector3(x+side*(w+.4f),y+1.1f,z+w+.4f),.07f,paint);for(int k=-1;k<=1;k++)Beam(r,"RailingPost",new Vector3(x+k*w,y,z+side*(w+.4f)),new Vector3(x+k*w,y+1.1f,z+side*(w+.4f)),.07f,paint);}}
   }
   for(int side=-1;side<=1;side+=2)Beam(r,"LadderRail",new Vector3(x+side*.33f,1,z-1.7f),new Vector3(x+side*.33f,61,z-1.7f),.07f,trim);for(int i=0;i<150;i++)Beam(r,"LadderRung",new Vector3(x-.33f,1+i*.4f,z-1.7f),new Vector3(x+.33f,1+i*.4f,z-1.7f),.045f,trim);
   Box(r,"StackCap",new Vector3(x,60.3f,z),new Vector3(2.6f,.3f,2.6f),trim);
  }
  for(int side=-1;side<=1;side+=2){for(int i=0;i<18;i++)Box(r,"SideCladdingSeam",new Vector3(side*26.04f,11,-12+i*1.45f),new Vector3(.08f,21,.035f),trim);for(int i=0;i<14;i++)Box(r,"WindowMullion",new Vector3(-24+i*3.7f,18.5f,side*13.17f),new Vector3(.10f,2.2f,.08f),trim);Box(r,"WindowSill",new Vector3(0,17.35f,side*13.16f),new Vector3(52,.16f,.25f),trim);}
  for(int i=0;i<3;i++){float x=-19+i*13;for(int k=0;k<6;k++)Box(r,"RoofVentLouvre",new Vector3(x,22.65f+k*.3f,-1.55f),new Vector3(4.5f,.12f,.10f),dark);}
  for(int i=0;i<10;i++)Box(r,"ServiceWingPanel",new Vector3(-25+i*4.2f,5.5f,-22.04f),new Vector3(.04f,10.5f,.08f),trim);
  for(int side=-1;side<=1;side+=2){Box(r,"Downpipe",new Vector3(side*25.5f,10.7f,-13.3f),new Vector3(.19f,21,.19f),steel);Box(r,"RoofDrain",new Vector3(side*25.5f,21.8f,-13.4f),new Vector3(.5f,.4f,.5f),steel);}
 }
 static void ExportObj(Mesh mesh,Material[] materials){var s=new System.Text.StringBuilder();var ci=System.Globalization.CultureInfo.InvariantCulture;foreach(var v in mesh.vertices)s.AppendLine("v "+v.x.ToString(ci)+" "+v.y.ToString(ci)+" "+v.z.ToString(ci));foreach(var uv in mesh.uv)s.AppendLine("vt "+uv.x.ToString(ci)+" "+uv.y.ToString(ci));foreach(var n in mesh.normals)s.AppendLine("vn "+n.x.ToString(ci)+" "+n.y.ToString(ci)+" "+n.z.ToString(ci));for(int sub=0;sub<mesh.subMeshCount;sub++){s.AppendLine("g "+materials[sub].name);s.AppendLine("usemtl "+materials[sub].name);var t=mesh.GetTriangles(sub);for(int i=0;i<t.Length;i+=3){s.Append("f");for(int j=0;j<3;j++){int k=t[i+j]+1;s.Append(" "+k+"/"+k+"/"+k);}s.AppendLine();}}File.WriteAllText(Root+"/UtilityPlant_Painter.obj",s.ToString());}
 static void Capture(Camera camera){var rt=new RenderTexture(1200,850,24);var old=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1200,850,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,850),0,0);image.Apply();File.WriteAllBytes("C:/Users/Admin/Documents/ChatGPT/안성제철/output/UtilityPlant_Preview.png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);}
}
}







