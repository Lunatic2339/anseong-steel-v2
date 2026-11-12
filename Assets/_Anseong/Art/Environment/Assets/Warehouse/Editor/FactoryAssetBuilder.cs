using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace CoastalFactory.Editor {
public static class FactoryAssetBuilder {
 const string Root="Assets/_Anseong/Art/Environment/Assets/Warehouse";
 const string Generated=Root+"/Generated";
 static Material metal,dark,glass,trim,rust,concrete; static int index;
 static readonly List<GameObject> parts=new List<GameObject>();
 static Material Material(string name,Color color,float smooth=0){string path=Generated+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;m.SetFloat("_Smoothness",smooth);AssetDatabase.CreateAsset(m,path);return m;}
 static GameObject Box(string name,Vector3 pos,Vector3 size,Material mat,Transform parent,bool collider=false){var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(parent,false);o.transform.localPosition=pos;o.transform.localScale=size;o.GetComponent<Renderer>().sharedMaterial=mat;if(!collider)Object.DestroyImmediate(o.GetComponent<Collider>());return o;}
 static GameObject Piece(string name,Vector3 pos,Vector3 size,Material mat,Transform parent){var o=Box(name,pos,size,mat,parent,true);var b=o.AddComponent<Rigidbody>();b.isKinematic=true;b.mass=Mathf.Max(80,size.x*size.y*size.z*12);b.interpolation=RigidbodyInterpolation.Interpolate;o.AddComponent<FactoryPiece>();parts.Add(o);return o;}
 // Each destructible chunk has one mesh and one material. Decorative ribs are baked into it.
 static void Bake(GameObject chunk){var filters=chunk.GetComponentsInChildren<MeshFilter>();var ci=new CombineInstance[filters.Length];for(int i=0;i<filters.Length;i++){ci[i].mesh=filters[i].sharedMesh;ci[i].transform=chunk.transform.worldToLocalMatrix*filters[i].transform.localToWorldMatrix;}
  var mesh=new Mesh();mesh.name="FactoryChunk_"+index++;mesh.CombineMeshes(ci,true,true);AssetDatabase.CreateAsset(mesh,Generated+"/"+mesh.name+".asset");
  for(int i=chunk.transform.childCount-1;i>=0;i--)Object.DestroyImmediate(chunk.transform.GetChild(i).gameObject);
  chunk.GetComponent<MeshFilter>().sharedMesh=mesh;
 }
 static void Wall(Transform parent,Vector3 pos,Vector3 size,bool window,string name){var p=Piece(name,pos,size,window?glass:metal,parent);
  // Local coordinates account for the base cube's non-unit scale.
  for(int i=-3;i<=3;i++)Box("Raised cladding seam",new Vector3(i/8f,0,-.62f),new Vector3(.012f,1,.25f),window?glass:metal,p.transform);
  if(window){Box("Window frame vertical",Vector3.zero,new Vector3(.025f,1,1.2f),glass,p.transform);Box("Window frame horizontal",Vector3.zero,new Vector3(1,.045f,1.2f),glass,p.transform);}
  Bake(p);
 }
 static void Beam(Transform parent,Vector3 a,Vector3 b,float thickness,string name){var d=b-a;var o=Piece(name,(a+b)/2,new Vector3(thickness,d.magnitude,thickness),dark,parent);o.transform.rotation=Quaternion.FromToRotation(Vector3.up,d);Bake(o);}
 [MenuItem("Anseong/Sandbox/Build Destructible Factory %#j")]
 public static void Build(){
  if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
  if(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Warehouse.prefab")!=null){EditorSceneManager.OpenScene("Assets/_Anseong/Scenes/Sandbox/Sandbox_FactoryDemo.unity");return;}
  Directory.CreateDirectory(Generated);AssetDatabase.Refresh();
  metal=Material("Weathered_Grey_Steel",new Color(.39f,.45f,.46f),.23f);dark=Material("Structural_Dark_Steel",new Color(.13f,.17f,.18f),.2f);glass=Material("Smoky_Blue_Glass",new Color(.12f,.25f,.29f),.65f);trim=Material("Safety_Ochre",new Color(.76f,.45f,.12f));rust=Material("Oxidised_Red",new Color(.36f,.17f,.12f));concrete=Material("Concrete",new Color(.34f,.35f,.33f));
  var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
  var root=new GameObject("Warehouse");var building=root.AddComponent<FactoryBuilding>();var shell=new GameObject("Destructible_Panels_and_Frame");shell.transform.SetParent(root.transform,false);parts.Clear();index=0;
  // End walls: 6 bays, 3 levels; the centre two bottom bays are loading shutters.
  for(int face=0;face<2;face++)for(int x=0;x<6;x++)for(int y=0;y<3;y++){
   bool door=y==0&&(x==2||x==3);bool window=y==2&&x!=0&&x!=5;
   Vector3 pos=new Vector3(-20+x*8,4+y*8,face==0?-16:16);
   if(door){var p=Piece("Loading_Shutter_"+face+"_"+x,pos,new Vector3(7.9f,8,.5f),dark,shell.transform);for(int k=0;k<10;k++)Box("Shutter slat",new Vector3(0,-.45f+k*.1f,-.6f),new Vector3(1,.013f,.2f),dark,p.transform);Bake(p);}
   else Wall(shell.transform,pos,new Vector3(7.98f,7.98f,.55f),window,"EndWall_"+face+"_"+x+"_"+y);
  }
  for(int side=0;side<2;side++)for(int z=0;z<4;z++)for(int y=0;y<3;y++)Wall(shell.transform,new Vector3(side==0?-24:24,4+y*8,-12+z*8),new Vector3(.55f,7.98f,7.98f),y==2,"SideWall_"+side+"_"+z+"_"+y);
  // Long-span steel shed roof, ridge 32m; trusses and panels are separate chunks.
  for(int x=0;x<6;x++)for(int side=0;side<2;side++){
   var panel=Piece("RoofPanel_"+x+"_"+side,new Vector3(-20+x*8,28,side==0?-8:8),new Vector3(7.98f,.4f,17.89f),metal,shell.transform);panel.transform.rotation=Quaternion.Euler(side==0?-26.565f:26.565f,0,0);Bake(panel);
  }
  for(int x=0;x<=6;x++){float px=-24+x*8;Beam(shell.transform,new Vector3(px,0,-16),new Vector3(px,24,-16),.7f,"Column_F_"+x);Beam(shell.transform,new Vector3(px,0,16),new Vector3(px,24,16),.7f,"Column_B_"+x);Beam(shell.transform,new Vector3(px,24,-16),new Vector3(px,32,0),.5f,"RoofTruss_F_"+x);Beam(shell.transform,new Vector3(px,24,16),new Vector3(px,32,0),.5f,"RoofTruss_B_"+x);Beam(shell.transform,new Vector3(px,24,-16),new Vector3(px,24,16),.4f,"RoofTie_"+x);}
  // Roof-end triangular gaps: steel lattice instead of solid walls.
  for(int side=0;side<2;side++)for(int z=-12;z<=12;z+=4){float h=8-Mathf.Abs(z)*.5f;Beam(shell.transform,new Vector3(side==0?-24:24,24,z),new Vector3(side==0?-24:24,24+h,z),.3f,"GableStud");}
  for(int z=-12;z<=12;z+=8)Beam(shell.transform,new Vector3(-24,24,z),new Vector3(24,24,z),.4f,"LongitudinalBeam");
  // Exterior roof vents and yellow door frame accents detach with the building.
  for(int x=-16;x<=16;x+=16){var v=Piece("RoofVent",new Vector3(x,33,0),new Vector3(4,2,3),rust,shell.transform);Bake(v);}
  for(int x=-8;x<=8;x+=16){var p=Piece("LoadingDoor_Jamb",new Vector3(x,5,-16.5f),new Vector3(.4f,10,.45f),trim,shell.transform);Bake(p);}
  var lintel=Piece("DoorLintel",new Vector3(0,10,-16.5f),new Vector3(16.5f,.45f,.45f),trim,shell.transform);Bake(lintel);
  var foundation=Box("Foundation",new Vector3(0,-.5f,0),new Vector3(50,1,34),concrete,root.transform,true);foundation.isStatic=true;
  var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Warehouse.prefab");Object.DestroyImmediate(root);
  var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);building=instance.GetComponent<FactoryBuilding>();
  Box("DemoGround",new Vector3(0,-1.1f,0),new Vector3(300,1,300),concrete,null,true).isStatic=true;
  var cam=Camera.main;cam.transform.position=new Vector3(72,48,-85);cam.transform.LookAt(new Vector3(0,13,0));cam.farClipPlane=1000;cam.fieldOfView=48;
  var demo=new GameObject("DemoControls_CLICK_hit_SPACE_collapse_R_reset").AddComponent<FactoryDemo>();demo.Configure(building,cam,false);
  var light=Object.FindFirstObjectByType<Light>();light.transform.rotation=Quaternion.Euler(45,-35,0);light.intensity=2;
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.5f,.55f);
  if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(new Vector3(0,12,0),Quaternion.Euler(25,-35,0),85);
  Selection.activeGameObject=instance;AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,"Assets/_Anseong/Scenes/Sandbox/Sandbox_FactoryDemo.unity");
  File.WriteAllText(Root+"/README.txt","Original procedural steelworks shed, 48x32m footprint, eaves24m/ridge32m, roof vents34m. URP. Play demo: click a wall to detach local chunks; Space staged full collapse; R reset. Physics debris freezes after12s. Pieces are authored regular modules, not arbitrary runtime mesh fracture. FactoryBuilding.ApplyImpact(worldPoint,strength,radius) is a local-only integration hook. Damage/NGO authority/VR performance not integrated or validated. Uniform root scaling allowed; nonuniform physics scaling not recommended. Drop Warehouse.prefab into Env_Steelworks/Breakables; root Y=0. Demo scene excluded from map. Generated primitives/materials authored with OpenAI Codex, 2026-10-27; no third-party model copied. Terms https://openai.com/policies/terms-of-use/ .");
  AssetDatabase.Refresh();Debug.Log("FACTORY_BUILD_READY pieces="+parts.Count);
 }
 [MenuItem("Anseong/Sandbox/Validate Factory In Play %#k")]
 public static void Validate(){if(EditorApplication.isPlaying)return;var demo=Object.FindFirstObjectByType<FactoryDemo>();var b=Object.FindFirstObjectByType<FactoryBuilding>();if(demo==null||b==null)return;demo.Configure(b,Camera.main,true);EditorApplication.isPlaying=true;}
 [MenuItem("Anseong/Sandbox/Capture Factory Preview %#u")]
 public static void Capture(){Directory.CreateDirectory("C:/Users/Admin/Documents/ChatGPT/안성제철/output");ScreenCapture.CaptureScreenshot("C:/Users/Admin/Documents/ChatGPT/안성제철/output/Factory_"+(Object.FindFirstObjectByType<FactoryBuilding>().DetachedCount>0?"Broken":"Intact")+".png");}
 [MenuItem("Anseong/Sandbox/Build Robot Fall Demo %#F8")]
 public static void BuildFall(){
  if(EditorApplication.isPlaying||!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
  var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
  concrete=AssetDatabase.LoadAssetAtPath<Material>(Generated+"/Concrete.mat");
  var b=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Warehouse.prefab"))).GetComponent<FactoryBuilding>();
  var d=new Vector3(.5f,0,-.8660254f);var right=Vector3.Cross(Vector3.up,d);
  b.transform.position=d*48+right*10;b.transform.rotation=Quaternion.LookRotation(d);
  Box("Ground",new Vector3(0,-1.1f,0),new Vector3(350,1,350),concrete,null,true);
  var steel=Material("RobotProxy_Blue",new Color(.14f,.28f,.40f),.4f);var joints=Material("RobotProxy_Joints",new Color(.08f,.10f,.12f));
  var robot=new GameObject("65m_ROBOT_FALL_PROXY_NotGameRobot");
  Box("Torso",new Vector3(0,39,0),new Vector3(14,22,9),steel,robot.transform);
  Box("Pelvis",new Vector3(0,26,0),new Vector3(12,6,8),joints,robot.transform);
  Box("Head",new Vector3(0,59,0),new Vector3(8,12,8),steel,robot.transform);
  for(int s=-1;s<=1;s+=2){Box("Leg",new Vector3(s*4,13,0),new Vector3(5,24,6),steel,robot.transform);Box("Foot",new Vector3(s*4,1.5f,2),new Vector3(6,3,10),joints,robot.transform);Box("Arm",new Vector3(s*10,36,0),new Vector3(5,23,6),steel,robot.transform);}
  var cam=Camera.main;cam.transform.position=new Vector3(34,70,80);cam.transform.LookAt(new Vector3(5,20,-25));cam.fieldOfView=48;cam.farClipPlane=1000;
  var demo=new GameObject("SPACE_Fall_R_Reset_65m_5oclock").AddComponent<RobotFallDemo>();Directory.CreateDirectory("C:/Users/Admin/Documents/ChatGPT/안성제철/output");demo.Configure(b,robot.transform,"C:/Users/Admin/Documents/ChatGPT/안성제철/output");
  var light=Object.FindFirstObjectByType<Light>();light.transform.rotation=Quaternion.Euler(50,-35,0);light.intensity=2;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.48f,.51f,.55f);
  EditorSceneManager.SaveScene(scene,"Assets/_Anseong/Scenes/Sandbox/Sandbox_RobotFall.unity");AssetDatabase.SaveAssets();Selection.activeGameObject=b.gameObject;
  Debug.Log("ROBOT_FALL_DEMO_READY footprint48x32 height32 robot65 fallDirection5oclock");
 }
 [MenuItem("Anseong/Sandbox/Validate Robot Fall %#t")]
 public static void ValidateFall(){if(EditorApplication.isPlaying)return;var demo=Object.FindFirstObjectByType<RobotFallDemo>();if(!demo)return;demo.SetValidation();EditorApplication.isPlaying=true;}
 [MenuItem("Anseong/Sandbox/Export Factory Package %#e")]
  public static void Export(){Directory.CreateDirectory("C:/Users/Admin/Documents/ChatGPT/안성제철/output");AssetDatabase.ExportPackage(new[]{Root,"Assets/_Anseong/Scenes/Sandbox/Sandbox_FactoryDemo.unity","Assets/_Anseong/Scenes/Sandbox/Sandbox_RobotFall.unity"},"C:/Users/Admin/Documents/ChatGPT/안성제철/output/Warehouse.unitypackage",ExportPackageOptions.Recurse);Debug.Log("FACTORY_PACKAGE_EXPORTED");}
}
}

