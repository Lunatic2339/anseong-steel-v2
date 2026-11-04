using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using AnseongSteel.Bosses;
using Object=UnityEngine.Object;
namespace BossSteelV13.Editor
{
 public static class SteelV13Installer
 {
  public const string Root="Assets/_BossSteelV13";
  public const string ScenePath=Root+"/BossSteelV13Test.unity";
  [MenuItem("Tools/Boss Steel V13/Open Animation Test")]
  public static void Open(){EditorSceneManager.OpenScene(ScenePath);}
  public static void Build(){
   AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
   var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"/MechaSteelV13.fbx");
   importer.importAnimation=false;importer.animationType=ModelImporterAnimationType.Generic;importer.optimizeGameObjects=false;importer.isReadable=true;importer.SaveAndReimport();
   foreach(string name in new[]{"BaseColor","MetallicSmoothness","Normal","Emission"}){
    var ti=(TextureImporter)AssetImporter.GetAtPath(Root+"/SteelV13_"+name+".png");ti.sRGBTexture=name=="BaseColor";ti.maxTextureSize=8192;ti.mipmapEnabled=true;ti.mipmapFilter=TextureImporterMipFilter.KaiserFilter;ti.filterMode=FilterMode.Trilinear;ti.anisoLevel=8;ti.alphaIsTransparency=false;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.alphaSource=TextureImporterAlphaSource.FromInput;
    if(name=="Normal")ti.textureType=TextureImporterType.NormalMap;ti.SaveAndReimport();
   }
   var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.name="SteelV13";
   material.SetTexture("_BaseMap",Texture("BaseColor"));material.SetColor("_BaseColor",Color.white);
   material.SetTexture("_MetallicGlossMap",Texture("MetallicSmoothness"));material.SetFloat("_Metallic",1);material.SetFloat("_Smoothness",1);material.EnableKeyword("_METALLICSPECGLOSSMAP");
   material.SetTexture("_BumpMap",Texture("Normal"));material.SetFloat("_BumpScale",1);material.EnableKeyword("_NORMALMAP");
   material.SetTexture("_EmissionMap",Texture("Emission"));material.SetColor("_EmissionColor",Color.white);material.EnableKeyword("_EMISSION");material=Save(material,Root+"/SteelV13.mat");
   EditorSceneManager.OpenScene("Assets/_BossPrototype/Scenes/BossModelTest.unity");
   EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
   var demo=Object.FindFirstObjectByType<BossModelDemo>();var driver=demo.driver;var animator=driver.animator;
   var instance=PrefabUtility.GetOutermostPrefabInstanceRoot(driver.gameObject);if(instance)PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
   var oldSkins=animator.GetComponentsInChildren<SkinnedMeshRenderer>();
   var inverseRest=new Dictionary<string,Matrix4x4>();
   foreach(var r in oldSkins)for(int i=0;i<r.bones.Length;i++)if(r.bones[i])inverseRest[r.bones[i].name]=r.sharedMesh.bindposes[i]*r.transform.worldToLocalMatrix;
   var canonical=animator.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("mixamorig:")).ToDictionary(t=>t.name);
   var fresh=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/MechaSteelV13.fbx"));
   PrefabUtility.UnpackPrefabInstance(fresh,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
   fresh.name="SteelV13_Visual";fresh.transform.SetParent(animator.transform,false);
   var importedAnimator=fresh.GetComponent<Animator>();if(importedAnimator)Object.DestroyImmediate(importedAnimator);
   int count=0;
   foreach(var r in fresh.GetComponentsInChildren<SkinnedMeshRenderer>()){
    if(r.transform!=fresh.transform)r.transform.SetParent(fresh.transform,true);
    Debug.Log("V13 IMPORT mesh bounds="+r.sharedMesh.bounds+" worldScale="+r.transform.lossyScale+" rotation="+r.transform.rotation.eulerAngles);
    var mesh=Object.Instantiate(r.sharedMesh);var bind=mesh.bindposes;var bones=r.bones;
    for(int i=0;i<bones.Length;i++){
     string name=bones[i].name;if(!canonical.ContainsKey(name)||!inverseRest.ContainsKey(name))throw new Exception("Unmapped bone "+name);
     bind[i]=inverseRest[name]*r.transform.localToWorldMatrix;bones[i]=canonical[name];
    }
    mesh.bindposes=bind;mesh=Save(mesh,Root+"/CompatibleSkin"+count+".asset");r.sharedMesh=mesh;r.bones=bones;r.rootBone=canonical["mixamorig:Hips"];r.sharedMaterials=new[]{material};r.updateWhenOffscreen=true;count++;
   }
   foreach(var old in oldSkins)old.enabled=false;
   // Keep the original skeleton and every gameplay reference, including hand poses and sword sockets.
   foreach(var t in fresh.GetComponentsInChildren<Transform>().Where(t=>t.name=="mixamorig:Hips").ToArray())Object.DestroyImmediate(t.gameObject);
   demo.autoDemo=true;demo.showControls=true;
   var idle=animator.runtimeAnimatorController.animationClips.First(c=>c.name=="Idle");idle.SampleAnimation(animator.gameObject,0);
   var skin=fresh.GetComponentInChildren<SkinnedMeshRenderer>();float height=SkinBounds(skin).size.y;
   Debug.Log("V13 world skin bounds="+SkinBounds(skin));
   if(height<1||height>6)throw new Exception("Invalid V13 height "+height);
   RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.60f,.65f,.72f);RenderSettings.ambientEquatorColor=new Color(.38f,.40f,.44f);RenderSettings.ambientGroundColor=new Color(.16f,.17f,.19f);
   var fill=new GameObject("V13 Preview Fill Light").AddComponent<Light>();fill.type=LightType.Directional;fill.color=new Color(.82f,.9f,1);fill.intensity=.8f;fill.shadows=LightShadows.None;fill.transform.rotation=Quaternion.LookRotation(-driver.transform.forward+Vector3.down*.65f);
   var cube=Save(MakeReflection(),Root+"/SteelReflection.asset");RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=cube;RenderSettings.reflectionIntensity=.8f;
   PrefabUtility.SaveAsPrefabAsset(driver.gameObject,Root+"/BossSteelV13.prefab");
   EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
   Directory.CreateDirectory("Library/BossSteelV13");Capture(Camera.main,"Library/BossSteelV13/scene.png");
   File.WriteAllText("Library/BossSteelV13/build.txt","PASS skins="+count+" bones="+canonical.Count+" height="+height+" clips="+animator.runtimeAnimatorController.animationClips.Length);
   Debug.Log("STEEL V13 BUILD PASS height="+height);
  }
  static Texture2D Texture(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/SteelV13_"+name+".png");
  static Bounds SkinBounds(SkinnedMeshRenderer r){var vertices=r.sharedMesh.vertices;var weights=r.sharedMesh.boneWeights;var matrices=r.bones.Select((b,i)=>b.localToWorldMatrix*r.sharedMesh.bindposes[i]).ToArray();Bounds result=default;for(int i=0;i<vertices.Length;i++){var w=weights[i];var p=matrices[w.boneIndex0].MultiplyPoint3x4(vertices[i])*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(vertices[i])*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(vertices[i])*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(vertices[i])*w.weight3;if(i==0)result=new Bounds(p,Vector3.zero);else result.Encapsulate(p);}return result;}
  static T Save<T>(T asset,string path) where T:Object{var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing){EditorUtility.CopySerialized(asset,existing);Object.DestroyImmediate(asset);EditorUtility.SetDirty(existing);return existing;}AssetDatabase.CreateAsset(asset,path);return asset;}
  static Cubemap MakeReflection(){
   int size=128;var cube=new Cubemap(size,TextureFormat.RGBAHalf,true);cube.name="Steel softbox reflections";
   for(int face=0;face<6;face++){var pixels=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++){
    float u=2f*(x+.5f)/size-1,v=2f*(y+.5f)/size-1;Vector3 d;
    switch(face){case 0:d=new Vector3(1,-v,-u);break;case 1:d=new Vector3(-1,-v,u);break;case 2:d=new Vector3(u,1,v);break;case 3:d=new Vector3(u,-1,-v);break;case 4:d=new Vector3(u,-v,1);break;default:d=new Vector3(-u,-v,-1);break;}
    d.Normalize();float a=Mathf.Pow(Mathf.Max(0,Vector3.Dot(d,new Vector3(-.6f,.65f,-.4f).normalized)),12);float b=Mathf.Pow(Mathf.Max(0,Vector3.Dot(d,new Vector3(.7f,.3f,.65f).normalized)),20);
    pixels[y*size+x]=new Color(.10f,.12f,.15f)+new Color(2.8f,2.55f,2.2f)*a+new Color(1.5f,2f,2.8f)*b;
   }cube.SetPixels(pixels,(CubemapFace)face);}cube.Apply(true,false);return cube;
  }
  public static void Capture(Camera cam,string path){
   var rt=new RenderTexture(1280,720,24);rt.Create();float aspect=cam.aspect;cam.aspect=16f/9f;
   RenderPipeline.SubmitRenderRequest(cam,new RenderPipeline.StandardRequest{destination=rt});var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());RenderTexture.active=old;cam.aspect=aspect;Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);
  }
 }
 [InitializeOnLoad]
 public static class SteelV13Verification{
  static SteelV13Verification(){EditorApplication.playModeStateChanged+=Changed;}
  public static void Run(){EditorSceneManager.OpenScene(SteelV13Installer.ScenePath);var demo=Object.FindFirstObjectByType<BossModelDemo>();demo.autoDemo=false;SessionState.SetBool("SteelV13Verify",true);EditorApplication.isPlaying=true;}
  static void Changed(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("SteelV13Verify",false)){SessionState.SetBool("SteelV13Verify",false);new GameObject("V13 verification").AddComponent<SteelV13Check>();}}
 }
 public sealed class SteelV13Check:MonoBehaviour{
  string failure;BossCombatController boss;int attacks;float deadline;
  IEnumerator Start(){Application.logMessageReceived+=Error;boss=Object.FindFirstObjectByType<BossCombatController>();deadline=Time.realtimeSinceStartup+240;yield return new WaitForSeconds(.5f);var routine=Checks();while(failure==null){bool more=false;object step=null;try{more=routine.MoveNext();if(more)step=routine.Current;}catch(Exception e){failure=e.ToString();}if(!more)break;yield return step;}Finish();}
  void Error(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception)&&!stack.Contains("UnityEditor.Search"))failure=message;}
  IEnumerator Checks(){
   var demo=Object.FindFirstObjectByType<BossModelDemo>();var skin=boss.driver.animator.transform.Find("SteelV13_Visual").GetComponentInChildren<SkinnedMeshRenderer>();
   if(!skin||skin.bones.Any(b=>!b))throw new Exception("Missing mesh/bones");
   demo.FaceTarget();while(boss.driver.IsTurning||boss.driver.rotation.IsRotating)yield return null;
   demo.WalkToTarget(2.8f);while(boss.driver.movement.IsMoving)yield return null;
   yield return new WaitForSeconds(.4f);SteelV13Installer.Capture(Camera.main,"Library/BossSteelV13/idle.png");
   foreach(var kind in new[]{BossAttackKind.HeavyPunch,BossAttackKind.SwordSlash,BossAttackKind.OverheadSmash,BossAttackKind.Grab,BossAttackKind.MissileBarrage,BossAttackKind.LaserSweep,BossAttackKind.EMP}){
    boss.ResetEncounter();boss.target.maximumHealth=100000;boss.target.ResetTarget();
    var definition=boss.attacks.First(d=>d.kind==kind);demo.FaceTarget();while(boss.driver.IsTurning||boss.driver.rotation.IsRotating)yield return null;demo.WalkToTarget(definition.preferredDistance);while(boss.driver.movement.IsMoving)yield return null;yield return new WaitForSeconds(.2f);
    if(!boss.TryAttack(kind))throw new Exception("Attack rejected "+kind+" "+boss.LastMessage);attacks++;bool captured=false;
    while(boss.IsBusy||boss.driver.IsAttacking){if(Time.realtimeSinceStartup>deadline)throw new Exception("Timeout "+kind);if(!captured&&boss.State==BossCombatState.Active){yield return new WaitForSeconds(.15f);SteelV13Installer.Capture(Camera.main,"Library/BossSteelV13/"+kind+".png");captured=true;}yield return null;}
    yield return new WaitForSeconds(.3f);
   }
  }
  void LateUpdate(){if(!boss)return;if(Time.realtimeSinceStartup>deadline&&deadline>0){failure="Timeout";Finish();return;}var t=boss.driver.animator.transform.Find("SteelV13_Visual");if(!t)return;foreach(var r in t.GetComponentsInChildren<SkinnedMeshRenderer>()){var m=new Mesh();r.BakeMesh(m);if(m.bounds.size.magnitude>10||float.IsNaN(m.bounds.size.x))failure="Invalid deformed bounds";Object.Destroy(m);}}
  void Finish(){Application.logMessageReceived-=Error;File.WriteAllText("Library/BossSteelV13/play.txt",failure==null&&attacks==7?"PASS: turn, walk, 7 attacks; mesh deformation remained bounded; original skeleton and gameplay retained.":"FAIL "+failure+" attacks="+attacks);EditorApplication.Exit(failure==null&&attacks==7?0:1);}
 }
}






