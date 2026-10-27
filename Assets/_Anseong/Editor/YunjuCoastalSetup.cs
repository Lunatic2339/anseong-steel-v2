using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Anseong.EditorTools {
public static class YunjuCoastalSetup {
const string Folder="Assets/_Anseong/Art/Environment/YunjuBase";
static Material Mat(string name, Color color, bool unlit=false) {
 string path=Folder+"/"+name+".mat";
 var m=AssetDatabase.LoadAssetAtPath<Material>(path);
 if(m==null){m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
 m.color=color; if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",0.15f);return m;
}
static GameObject Group(string n,Transform p=null){var g=new GameObject(n);if(p)g.transform.SetParent(p,false);return g;}
static GameObject Box(string n,Vector3 pos,Vector3 size,Material m,Transform p,bool collider=false){
 var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.position=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;
 if(!collider)Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;return g;
}
static void Label(string text,Vector3 pos,Transform p,Color color,float size=5){var g=Group(text,p);g.transform.position=pos;g.transform.rotation=Quaternion.Euler(90,0,0);var t=g.AddComponent<TextMesh>();t.text=text;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.fontSize=64;t.characterSize=size/10;t.color=color;}
[MenuItem("Anseong/Sandbox/Create Yunju Coastal Base %#y")]
public static void Build(){
 string scenePath="Assets/_Anseong/Scenes/Sandbox/Sandbox_Yunju.unity";
 if(File.Exists(scenePath)){Debug.LogWarning("Yunju scene already exists; not overwritten.");return;}
 if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
 Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
 var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
 var env=Group("Env_Steelworks");
 var ground=Group("Ground",env.transform);Group("Props",env.transform);Group("Breakables",env.transform);Group("Background",env.transform);Group("Far",env.transform);Group("Hangar",env.transform);
 var concrete=Mat("Concrete",new Color(.40f,.42f,.43f));var sea=Mat("Sea_Placeholder",new Color(.05f,.27f,.38f));var quay=Mat("Quay",new Color(.29f,.31f,.32f));
 var yellow=Mat("Guide_Yellow",new Color(1,.73f,.12f),true);var grid=Mat("Guide_Grid",new Color(.53f,.56f,.56f),true);
 Box("Yard_425x705m_TopY0",new Vector3(-100,-2,0),new Vector3(425,4,705),concrete,ground.transform,true);
 Box("Sea_Placeholder_NoCollider",new Vector3(1112.5f,-3,0),new Vector3(2000,.1f,3000),sea,ground.transform);
 Box("Quay_Edge_X112.5",new Vector3(112.5f,-3,0),new Vector3(2,6,705),quay,ground.transform,true);
 var guides=Group("LayoutGuides_HIDE_FOR_FINAL");
 for(int x=-50;x<=50;x+=25)Box("Grid_X"+x,new Vector3(x,.012f,0),new Vector3(.15f,.015f,545),grid,guides.transform);
 for(int z=-250;z<=250;z+=25)Box("Grid_Z"+z,new Vector3(0,.012f,z),new Vector3(125,.015f,.15f),grid,guides.transform);
 Box("Combat_Left",new Vector3(-62.5f,.035f,0),new Vector3(.65f,.025f,545),yellow,guides.transform);
 Box("Combat_Right",new Vector3(62.5f,.035f,0),new Vector3(.65f,.025f,545),yellow,guides.transform);
 Box("Combat_Back",new Vector3(0,.035f,-272.5f),new Vector3(125,.025f,.65f),yellow,guides.transform);
 Box("Combat_Front",new Vector3(0,.035f,272.5f),new Vector3(125,.025f,.65f),yellow,guides.transform);
 Label("COMBAT AREA 125 x 545 m",new Vector3(0,.08f,-240),guides.transform,Color.white,8);
 Label("SEA >",new Vector3(91,.08f,-110),guides.transform,Color.white,5);
 var refs=Group("ScaleReferences_HIDE_FOR_FINAL");
 var art=RobotArtSettings.Load();
 if(art.PlayerModel!=null){var r=(GameObject)PrefabUtility.InstantiatePrefab(art.PlayerModel);r.name="Player_REFERENCE_65m_Origin";r.transform.SetParent(refs.transform);r.transform.position=Vector3.zero;RobotSandboxUtility.FitToHeight(r,65,out _,out _,out _);}
 if(art.BossModel!=null){var r=(GameObject)PrefabUtility.InstantiatePrefab(art.BossModel);r.name="Boss_REFERENCE_65m_Z40";r.transform.SetParent(refs.transform);r.transform.position=new Vector3(0,0,40);r.transform.rotation=Quaternion.Euler(0,180,0);RobotSandboxUtility.FitToHeight(r,65,out _,out _,out _);}
 var refmat=Mat("Reference",new Color(.8f,.3f,.12f));
 Box("Truck_Size_2.5x4x10m",new Vector3(-40,2,-20),new Vector3(2.5f,4,10),refmat,refs.transform);
 var person=GameObject.CreatePrimitive(PrimitiveType.Capsule);person.name="Person_REFERENCE_1.6m";person.transform.SetParent(refs.transform);person.transform.position=new Vector3(-35,.8f,-20);person.transform.localScale=new Vector3(.4f,.8f,.4f);Object.DestroyImmediate(person.GetComponent<Collider>());
 Group("PilotEye_REFERENCE_Y60",refs.transform).transform.position=new Vector3(0,60,0);
 var cam=Camera.main;cam.transform.position=new Vector3(170,240,-380);cam.transform.LookAt(new Vector3(0,0,30));cam.farClipPlane=4000;
 var pilot=Group("PilotPreviewCamera_Y60",refs.transform).AddComponent<Camera>();pilot.transform.position=new Vector3(0,60,0);pilot.transform.rotation=Quaternion.Euler(12,0,0);pilot.farClipPlane=4000;pilot.enabled=false;
 AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,scenePath);
 if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,20),Quaternion.Euler(42,-25,0),350);
 Selection.activeGameObject=env;
 File.WriteAllText(Folder+"/SETUP.txt","Combat: X +/-62.5, Z +/-272.5, Y=0. Player origin; boss Z40 (reference only). Land X -312.5..112.5, Z +/-352.5. Sea begins X112.5. Put assets into Env_Steelworks/Props, Breakables, Background, Far, Hangar. Hide LayoutGuides and ScaleReferences for final export. Sea is a static placeholder, not gameplay water. Pilot preview camera is disabled; use it for manual viewing. No movement bounds or combat gameplay were changed.");
 AssetDatabase.Refresh();Debug.Log("YUNJU_COASTAL_BASE_READY "+scenePath);
}
}}
