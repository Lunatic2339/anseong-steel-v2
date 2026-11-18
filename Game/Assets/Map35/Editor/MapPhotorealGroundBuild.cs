using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Obj=UnityEngine.Object;

namespace Anseong.Map35.Editor
{
 public static partial class Map35Build
 {
  const string PhotoFolder="Assets/Map35/PhotorealGround";
  const string PhotoSource="Assets/ThirdParty/FreePBR/PhotorealGround/";
  static string PhotoOutput=>Path.GetFullPath("../docs/validation/map-photoreal-z1");
  static readonly int[] PhotoViews={2,5,9,28,35,37,39,40,42,3,12,14,30,36};

  [MenuItem("Anseong/Map35/Apply Photoreal Ground And Linear")]
  public static void ApplyPhotorealGroundAndLinear()
  {
   EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();Directory.CreateDirectory(PhotoOutput);
   string backup=Path.GetFullPath("../SourceAssets/PhotorealGround/BeforeApplication");Directory.CreateDirectory(backup);
   if(!File.Exists(backup+"/Steelworks35m_WideRuins.unity"))
   {
    File.Copy(WideScene,backup+"/Steelworks35m_WideRuins.unity");
    File.Copy("ProjectSettings/ProjectSettings.asset",backup+"/ProjectSettings.asset");
    File.WriteAllText(PhotoOutput+"/before-scene-sha256.txt",AuditHash(WideScene));
    CapturePhotoViews("before");
   }
   string colliderBefore=GroundColliderSignature(),anchorsBefore=PhotoAnchorSignature();
   ImportPhotoSources();ApplyPhotographicZ1Surfaces();BuildPhotorealZ1Edges();
   AddPhotoInspectionCameras();ValidateFiniteMapGeometry();
   bool colliders=colliderBefore==GroundColliderSignature(),anchors=anchorsBefore==PhotoAnchorSignature();
   if(!colliders||!anchors)throw new InvalidOperationException("Photoreal pass changed collider or scenario anchor state");
   File.WriteAllText(PhotoOutput+"/scope-integrity.txt","Existing colliders unchanged="+colliders+"\nScenario target transforms unchanged="+anchors+"\nB02-B03 ground and Z1 service road, no boss/role mechanics changed.\n");
   viewer.scenario.SelectStep(0);viewer.scenario.SelectRole(0);
   EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
   PlayerSettings.colorSpace=ColorSpace.Linear;AssetDatabase.SaveAssets();
   Debug.Log("PHOTOREAL_APPLIED_RESTART_FOR_LINEAR colliders_unchanged="+colliders+" anchors_unchanged="+anchors);
  }

  static void ImportPhotoSources()
  {
   foreach(string file in Directory.GetFiles(PhotoSource,"*",SearchOption.AllDirectories).Where(p=>p.EndsWith(".png")||p.EndsWith(".jpg")))
   {
    string path=file.Replace('\\','/');var importer=(TextureImporter)AssetImporter.GetAtPath(path);
    bool normal=path.Contains("_nor_gl_"),color=path.Contains("_diff_");
    importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
    importer.sRGBTexture=color;importer.maxTextureSize=path.Contains("_4k")?4096:2048;
    importer.mipmapEnabled=true;importer.anisoLevel=8;importer.wrapMode=TextureWrapMode.Repeat;
    importer.filterMode=FilterMode.Trilinear;importer.textureCompression=TextureImporterCompression.CompressedHQ;
    importer.isReadable=path.Contains("gravel_concrete_02")&&(path.Contains("rough")||path.Contains("disp"));
    importer.SaveAndReimport();
   }
  }
  static Texture2D PhotoTexture(string id,string channel)
  {
   string res=id=="aerial_asphalt_01"?"4k":"2k",ext=channel=="diff"?"jpg":"png";
   var t=AssetDatabase.LoadAssetAtPath<Texture2D>(PhotoSource+id+"/"+id+"_"+channel+"_"+res+"."+ext);
   if(t==null)throw new InvalidOperationException("Missing downloaded photo texture "+id+"/"+channel);return t;
  }
  static Texture2D PhotoDamageData()
  {
   var rough=PhotoTexture("gravel_concrete_02","rough");var height=PhotoTexture("gravel_concrete_02","disp");
   var pixels=rough.GetPixels32();var h=height.GetPixels32();
   for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(pixels[i].r,h[i].r,255,255);
   var image=new Texture2D(rough.width,rough.height,TextureFormat.RGBA32,false,true);
   image.SetPixels32(pixels);image.Apply();string path=PhotoFolder+"/ScannedAggregate_RoughHeight.png";
   File.WriteAllBytes(path,image.EncodeToPNG());Obj.DestroyImmediate(image);AssetDatabase.ImportAsset(path);
   var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=false;importer.maxTextureSize=2048;
   importer.mipmapEnabled=true;importer.anisoLevel=8;importer.wrapMode=TextureWrapMode.Repeat;
   importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
   return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
  }
  static Texture2D PhotoUseMask()
  {
   const int width=2048,height=1536;var pixels=new Color32[width*height];
   var old=AssetDatabase.LoadAssetAtPath<Texture2D>(PurposeGroundFolder+"/Masks/Z1_authored_surface_use.asset");
   for(int z=0;z<height;z++)for(int x=0;x<width;x++)
   {
    float px=-145+(x+.5f)/width*285,pz=-20+(z+.5f)/height*225;
    Color c=old.GetPixelBilinear((px+260)/530,(pz+140)/340);
    float grain=Mathf.PerlinNoise(px*.34f+13,pz*.31f+8);
    float broken=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.34f,.72f,grain));
    float eastB02=Mathf.Clamp01(1-Mathf.Abs(px+65)/5)*Mathf.Clamp01((pz-12)/4)*Mathf.Clamp01((82-pz)/5);
    float westB03=Mathf.Clamp01(1-Mathf.Abs(px-65)/4)*Mathf.Clamp01((pz-15)/4)*Mathf.Clamp01((84-pz)/4);
    float northJoint=Mathf.Clamp01(1-Mathf.Abs(pz-97)/5)*Mathf.Clamp01((60-Mathf.Abs(px))/5);
    float maintenance=Mathf.Clamp01(1-Vector2.Distance(new Vector2(px,pz),new Vector2(-78,50))/13);
    float service=Mathf.Max(Mathf.Max(eastB02,westB03),Mathf.Max(northJoint*.8f,maintenance*.75f));
    c.r=service*(.35f+.65f*broken);pixels[z*width+x]=c;
   }
   var mask=new Texture2D(width,height,TextureFormat.RGBA32,true,true){name="Z1 photographic use and exposed aggregate",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear};
   mask.SetPixels32(pixels);mask.Apply();string path=PhotoFolder+"/Z1_PhotoUse.asset";
   var saved=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
   if(saved==null){AssetDatabase.CreateAsset(mask,path);saved=mask;}else{EditorUtility.CopySerialized(mask,saved);Obj.DestroyImmediate(mask);EditorUtility.SetDirty(saved);}return saved;
  }
  public static void ApplyPhotographicZ1Surfaces()
  {
   PurposeFolder(PhotoFolder+"/Materials");var shader=Shader.Find("Anseong/PhotographicGround");
   if(shader==null||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Photographic ground shader not available");
   var data=PhotoDamageData();var mask=PhotoUseMask();var report=new StringBuilder("renderer,source,material,base_metres,broad_metres\n");int count=0;
   foreach(var r in Obj.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
   {
    var bounds=r.bounds;if(bounds.max.y>1.1f||bounds.size.y>1.5f)continue;
    bool local=bounds.min.x>=-140&&bounds.max.x<=130&&bounds.min.z>=-5&&bounds.max.z<=181;
    bool crossRoad=r.name.StartsWith("industrial cross road",StringComparison.Ordinal)&&bounds.center.z<110;
    if(!local&&!crossRoad)continue;var slots=r.sharedMaterials;
    for(int i=0;i<slots.Length;i++)
    {
     var old=slots[i];if(old==null||(old.shader.name!=PurposeGroundShader&&old.shader.name!=shader.name))continue;
     string source=old.GetTag("PurposeGroundSource",false);var src=AssetDatabase.LoadAssetAtPath<Material>(source);
     if(src==null)throw new InvalidOperationException("Cannot resolve source of "+r.name);
     string sourceName=src.name.ToLowerInvariant();if(sourceName.Contains("slag")||sourceName.Contains("tidal"))continue;
     bool road=sourceName.Contains("asphalt");string id=Hash128.Compute(PurposeGroundPath(r.transform)+"/"+i).ToString().Substring(0,16);
     string path=PhotoFolder+"/Materials/Photo_"+id+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
     if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}else material.shader=shader;
     string close=road?"Asphalt030":"Concrete040";
     material.SetTexture("_BaseTex",GroundChannel(close,"Color"));material.SetTexture("_BaseNormal",GroundChannel(close,"NormalGL"));
     material.SetTexture("_BaseRoughness",GroundChannel(close,"Roughness"));
     material.SetTexture("_DamageTex",PhotoTexture("gravel_concrete_02","diff"));material.SetTexture("_DamageNormal",PhotoTexture("gravel_concrete_02","nor_gl"));material.SetTexture("_DamageData",data);
     material.SetTexture("_MacroTex",PhotoTexture("aerial_asphalt_01","diff"));material.SetTexture("_MacroNormal",PhotoTexture("aerial_asphalt_01","nor_gl"));material.SetTexture("_MacroRoughness",PhotoTexture("aerial_asphalt_01","rough"));
     material.SetTexture("_PurposeMask",mask);material.SetVector("_MaskRect",new Vector4(-145,-20,1f/285,1f/225));
     material.SetFloat("_BaseMetres",road?2.2f:1.4f);material.SetFloat("_MacroMetres",30);
     material.SetFloat("_Asphalt",road?1:0);material.SetFloat("_MacroStrength",.26f);material.SetFloat("_NormalStrength",road?.65f:.68f);
     material.SetFloat("_RoughnessScale",.95f);material.SetFloat("_WetGain",.35f);material.SetColor("_Tint",road?new Color(.96f,.98f,1):new Color(.95f,.97f,.98f));
     material.SetOverrideTag("PurposeGroundSource",source);material.enableInstancing=true;EditorUtility.SetDirty(material);slots[i]=material;count++;
     report.AppendLine(r.name.Replace(',','_')+","+src.name+","+path+","+(road?"2.2":"1.4")+",30");
    }
    r.sharedMaterials=slots;
   }
   if(count<3)throw new InvalidOperationException("Too few Z1 surfaces assigned: "+count);
   Directory.CreateDirectory(PhotoOutput);File.WriteAllText(PhotoOutput+"/surface-assignments.csv",report.ToString());
   Debug.Log("PHOTOGRAPHIC_SURFACES_ASSIGNED slots="+count);
  }
  static void AddPhotoInspectionCameras()
  {
   if(viewer.views.Any(v=>v.name=="PHOTO B02 pavement connection"))
   {
    foreach(var c in viewer.views.Where(v=>v.name.StartsWith("PHOTO ",StringComparison.Ordinal)))
    {c.clearFlags=CameraClearFlags.Skybox;c.backgroundColor=RenderSettings.fogColor;}
    return;
   }
   cameraList.Clear();cameraList.AddRange(viewer.views);cameras=GameObject.Find("08 Review cameras").transform;
   MakeCamera("PHOTO B02 pavement connection",new Vector3(-57,2.2f,17),new Vector3(-66,.1f,35),60);
   MakeCamera("PHOTO B03 drainage crossing",new Vector3(57,2.4f,43),new Vector3(67,.15f,54),60);
   MakeCamera("PHOTO Mech maintenance yard",new Vector3(0,29.75f,76),new Vector3(-57,.2f,40),65);
   viewer.views=cameraList.ToArray();
   foreach(var c in viewer.views.Where(v=>v.name.StartsWith("PHOTO ",StringComparison.Ordinal)))
   {c.clearFlags=CameraClearFlags.Skybox;c.backgroundColor=RenderSettings.fogColor;}
   SetupReviewPostProcessing();
  }
  static string PhotoAnchorSignature()
  {return string.Join("\n",viewer.scenario.steps.SelectMany(s=>s.targets).Select(t=>t.name+"/"+t.position.ToString("F5")+"/"+t.rotation.ToString("F5")).OrderBy(x=>x,StringComparer.Ordinal));}
  static void CapturePhotoViews(string phase)
  {
   WarmSceneParticles();var states=new List<(GameObject,bool)>();
   foreach(var go in new[]{viewer.scenario.gameObject,proxies.gameObject,viewer.labels,viewer.routes,viewer.frusta,viewer.reservations})
   {states.Add((go,go.activeSelf));go.SetActive(false);}
   Camera temporary=null;
   try
   {
    foreach(int i in PhotoViews){viewer.Select(i);SaveWearCamera(viewer.views[i],PhotoOutput+"/"+phase+"-"+i.ToString("00")+".png");}
    temporary=Obj.Instantiate(viewer.views[37]);temporary.enabled=false;
    var pos=new[]{new Vector3(-57,2.2f,17),new Vector3(57,2.4f,43),new Vector3(0,29.75f,76)};
    var look=new[]{new Vector3(-66,.1f,35),new Vector3(67,.15f,54),new Vector3(-57,.2f,40)};
    for(int i=0;i<3;i++){temporary.transform.position=pos[i];temporary.transform.LookAt(look[i]);temporary.fieldOfView=i==2?65:60;SaveWearCamera(temporary,PhotoOutput+"/"+phase+"-near"+i+".png");}
   }
   finally{if(temporary!=null)Obj.DestroyImmediate(temporary.gameObject);foreach(var pair in states)pair.Item1.SetActive(pair.Item2);}
  }
  [MenuItem("Anseong/Map35/Bake Build Photoreal Ground")]
  public static void BakeBuildPhotorealGround()
  {
   if(PlayerSettings.colorSpace!=ColorSpace.Linear)throw new InvalidOperationException("Fresh Linear editor required");
   if(ShaderUtil.ShaderHasError(Shader.Find("Anseong/PhotographicGround")))throw new InvalidOperationException("Photographic shader errors in Linear editor");
   EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();viewer.scenario.SelectStep(0);
   SetPhotographicSky();ConfigureProductionLook();PrepareZ1Lighting();AssetDatabase.SaveAssets();
   EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);Debug.Log("PHOTO_LINEAR_BAKE_START");
   if(!Lightmapping.Bake()||LightmapSettings.lightmaps.Length==0)throw new InvalidOperationException("Linear GI bake failed");
   WarmSceneParticles();BakeLocalReflections();viewer.scenario.SelectStep(0);AssetDatabase.SaveAssets();
   EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
   CapturePhotoViews("after");
   if(ShaderUtil.ShaderHasError(Shader.Find("Anseong/PhotographicGround")))throw new InvalidOperationException("Photographic shader errors after Linear capture");
   ValidateScenarioSpatialChecks(viewer.scenario);ValidateMaintenanceAccess();
   viewer.scenario.SelectStep(0);viewer.scenario.SelectRole(0);EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
   var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{WideScene},locationPathName="../Builds/MapWide/AnseongMapWide.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
   if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Photoreal viewer build failed");
   File.WriteAllText(PhotoOutput+"/build-validation.txt","Color space="+PlayerSettings.colorSpace+"\nLightmap count="+LightmapSettings.lightmaps.Length+"\nCameras="+viewer.views.Length+"\nShader errors="+ShaderUtil.ShaderHasError(Shader.Find("Anseong/PhotographicGround"))+"\nScene SHA256="+AuditHash(WideScene)+"\n17 before/17 after mono Editor captures; before Gamma, after Linear plus refreshed GI/reflections. HMD and co-op gameplay not tested.\n");
   Debug.Log("PHOTOREAL_BUILD_OK cameras="+viewer.views.Length);
  }
  [MenuItem("Anseong/Map35/Refine Photoreal Surface Calibration")]
  public static void RefinePhotorealSurfaceCalibration()
  {
   string previous=PhotoOutput+"/initial-calibration";Directory.CreateDirectory(previous);
   foreach(string path in Directory.GetFiles(PhotoOutput,"after-*.png"))
    if(!File.Exists(previous+"/"+Path.GetFileName(path)))File.Copy(path,previous+"/"+Path.GetFileName(path));
   foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{PhotoFolder+"/Materials"}))
   {
    var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
    m.SetFloat("_MacroStrength",.26f);EditorUtility.SetDirty(m);
   }
   AssetDatabase.SaveAssets();BakeBuildPhotorealGround();
  }
  [MenuItem("Anseong/Map35/Finalize Photoreal Review Cameras")]
  public static void FinalizePhotorealReviewCameras()
  {
   EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();AddPhotoInspectionCameras();
   viewer.scenario.SelectStep(0);viewer.scenario.SelectRole(0);
   EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);BuildScenarioViewerOnly();
   string report=File.ReadAllText(PhotoOutput+"/build-validation.txt");
   report=System.Text.RegularExpressions.Regex.Replace(report,"Scene SHA256=[^\\r\\n]*","Scene SHA256="+AuditHash(WideScene));
   File.WriteAllText(PhotoOutput+"/build-validation.txt",report+"Review camera clear flags corrected to Skybox for PHOTO47/48/49. No lighting/material changes in this follow-up.\n");
   Debug.Log("PHOTOREAL_REVIEW_CAMERAS_FIXED skybox=3");
  }
 }
}
