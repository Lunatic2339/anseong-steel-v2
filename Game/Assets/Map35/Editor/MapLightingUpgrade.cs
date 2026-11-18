using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using Obj=UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        const string LightingRoot=Base+"Lighting/";
        static void ConfigureProductionLook()
        {
            Directory.CreateDirectory(LightingRoot);AssetDatabase.Refresh();
            var sun=Obj.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l=>l.type==LightType.Directional);
            sun.name="Overcast directional break in clouds";sun.intensity=.42f;sun.color=new Color(.94f,.96f,1);
            sun.shadows=LightShadows.Soft;sun.shadowStrength=.48f;sun.shadowBias=.03f;sun.shadowNormalBias=.18f;
            sun.lightmapBakeType=LightmapBakeType.Mixed;sun.bounceIntensity=1;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.61f,.64f,.68f);
            RenderSettings.ambientEquatorColor=new Color(.45f,.47f,.49f);
            RenderSettings.ambientGroundColor=new Color(.27f,.26f,.24f);
            RenderSettings.reflectionIntensity=.8f;
            RenderSettings.fogColor=new Color(.46f,.49f,.51f);RenderSettings.fogStartDistance=420;RenderSettings.fogEndDistance=2700;
            QualitySettings.pixelLightCount=4;QualitySettings.shadowDistance=260;
            var ocean=AssetDatabase.LoadAssetAtPath<Material>(Base+"Materials/Wide_storm_sea.mat");
            if(ocean!=null)ocean.SetTexture("_ReflectionCube",RenderSettings.customReflectionTexture);
            foreach(var ps in Obj.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {var main=ps.main;main.playOnAwake=true;main.prewarm=true;}
            SetupReviewPostProcessing();
        }
        static void SetupReviewPostProcessing()
        {
            string profilePath=LightingRoot+"OvercastReviewProfile.asset";
            var profile=AssetDatabase.LoadAssetAtPath<PostProcessProfile>(profilePath);
            if(profile==null){profile=ScriptableObject.CreateInstance<PostProcessProfile>();AssetDatabase.CreateAsset(profile,profilePath);}
            foreach(var setting in profile.settings.ToArray())Obj.DestroyImmediate(setting,true);profile.settings.Clear();
            var grade=profile.AddSettings<ColorGrading>();grade.enabled.Override(true);grade.gradingMode.Override(GradingMode.HighDefinitionRange);
            grade.tonemapper.Override(Tonemapper.Neutral);grade.postExposure.Override(.15f);grade.saturation.Override(-5);grade.contrast.Override(3);
            var bloom=profile.AddSettings<Bloom>();bloom.enabled.Override(true);bloom.intensity.Override(.35f);bloom.threshold.Override(1.2f);bloom.diffusion.Override(5);bloom.fastMode.Override(true);
            var ao=profile.AddSettings<AmbientOcclusion>();ao.enabled.Override(true);ao.mode.Override(AmbientOcclusionMode.ScalableAmbientObscurance);
            ao.intensity.Override(.55f);ao.radius.Override(1.1f);ao.quality.Override(AmbientOcclusionQuality.Medium);
            foreach(var setting in profile.settings)if(!AssetDatabase.Contains(setting))AssetDatabase.AddObjectToAsset(setting,profile);
            EditorUtility.SetDirty(profile);
            var volume=Obj.FindFirstObjectByType<PostProcessVolume>()??new GameObject("Desktop review grading and contact AO").AddComponent<PostProcessVolume>();volume.isGlobal=true;volume.sharedProfile=profile;volume.priority=1;
            var resources=AssetDatabase.LoadAssetAtPath<PostProcessResources>("Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset");
            if(resources==null)throw new InvalidOperationException("Post Processing resources missing");
            foreach(var camera in viewer.views)
            {
                camera.allowHDR=true;camera.allowMSAA=true;camera.renderingPath=RenderingPath.Forward;
                var layer=camera.GetComponent<PostProcessLayer>()??camera.gameObject.AddComponent<PostProcessLayer>();
                layer.Init(resources);layer.volumeLayer=1;layer.volumeTrigger=camera.transform;
                layer.antialiasingMode=PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;layer.stopNaNPropagation=true;
            }
        }
        static void PrepareZ1Lighting()
        {
            var roots=new[]{Existing("B01"),Existing("B02"),Existing("B03"),GameObject.Find("I1 fixed rail and role equipment")?.transform,
                GameObject.Find("Photoreal Z1 ground connections")?.transform}.Where(t=>t!=null).ToArray();var used=new HashSet<Mesh>();int marked=0;
            foreach(var renderer in Obj.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
            {
                if(renderer.transform.IsChildOf(proxies)||renderer.transform.IsChildOf(labels)||renderer.transform.IsChildOf(routes)||renderer.transform.IsChildOf(volumes))continue;
                if(viewer.scenario!=null&&viewer.scenario.steps.Any(s=>renderer.transform.IsChildOf(s.root.transform)))
                {GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,0);renderer.receiveGI=ReceiveGI.LightProbes;continue;}
                if(renderer.GetComponent<TextMesh>()!=null)continue;
                var flags=(GameObjectUtility.GetStaticEditorFlags(renderer.gameObject)&~(StaticEditorFlags.ContributeGI|StaticEditorFlags.BatchingStatic))|StaticEditorFlags.ReflectionProbeStatic;
                renderer.receiveGI=ReceiveGI.LightProbes;
                if(renderer.transform.IsChildOf(structures)&&!renderer.transform.IsChildOf(viewer.coil)&&!renderer.transform.IsChildOf(viewer.plate))flags|=StaticEditorFlags.BatchingStatic;
                bool local=roots.Any(root=>renderer.transform.IsChildOf(root));
                var b=renderer.bounds;
                // Standalone B03 hardstanding spans x=62..119 (centre 90.5),
                // outside its building root. Include it with the other Z1 aprons;
                // the size/height/Z gates still exclude the large site ground.
                bool apron=(b.center.y<2&&b.size.y<2&&b.size.x<180&&b.size.z<180&&b.center.x>-140&&b.center.x<125&&b.center.z>-75&&b.center.z<120);
                bool lod1=renderer.transform.GetComponentsInParent<Transform>().Any(t=>t.name=="LOD1 combined structure");
                bool fineJoint=renderer.name.Contains("expansion joints");
                if((local||apron)&&!lod1&&!fineJoint&&!renderer.transform.IsChildOf(viewer.plate)&&!renderer.transform.IsChildOf(viewer.coil)&&!renderer.transform.IsChildOf(viewer.lateCoil.transform))
                {
                    var filter=renderer.GetComponent<MeshFilter>();
                    if(filter!=null&&filter.sharedMesh!=null)
                    {
                        var mesh=filter.sharedMesh;
                        if(mesh.uv2.Length!=mesh.vertexCount)
                        {
                            // Lightmap UV generation must never rewrite an acquired/source FBX or Unity primitive.
                            string key=MeshKey(mesh,Vector3.one);string path=LightingRoot+"BakeMesh_"+key+".asset";
                            var copy=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                            if(copy==null){copy=Obj.Instantiate(mesh);copy.name=mesh.name+" lightmap copy";Unwrapping.GenerateSecondaryUVSet(copy);AssetDatabase.CreateAsset(copy,path);}
                            filter.sharedMesh=copy;mesh=copy;
                        }
                        used.Add(mesh);flags|=StaticEditorFlags.ContributeGI;renderer.receiveGI=ReceiveGI.Lightmaps;
                        renderer.scaleInLightmap=b.size.magnitude<3?.3f:1;marked++;
                    }
                }
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,flags);
            }
            var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingRoot+"Z1_Indirect.lighting");
            if(settings==null){settings=new LightingSettings();AssetDatabase.CreateAsset(settings,LightingRoot+"Z1_Indirect.lighting");}
            settings.bakedGI=true;settings.realtimeGI=false;settings.lightmapper=LightingSettings.Lightmapper.ProgressiveCPU;
            settings.mixedBakeMode=MixedLightingMode.IndirectOnly;settings.directionalityMode=LightmapsMode.CombinedDirectional;
            settings.lightmapResolution=3;settings.lightmapMaxSize=1024;settings.lightmapPadding=3;
            settings.directSampleCount=32;settings.indirectSampleCount=128;settings.environmentSampleCount=128;settings.maxBounces=3;
            settings.ao=true;settings.aoMaxDistance=1.5f;settings.aoExponentIndirect=.6f;settings.aoExponentDirect=0;
            settings.indirectScale=1;settings.albedoBoost=1;Lightmapping.lightingSettings=settings;
            foreach(var old in Obj.FindObjectsByType<LightProbeGroup>(FindObjectsSortMode.None))if(old.name=="Z1 indirect light probes")Obj.DestroyImmediate(old.gameObject);
            var group=new GameObject("Z1 indirect light probes").AddComponent<LightProbeGroup>();var points=new List<Vector3>();
            foreach(float x in new[]{-118f,-94,-70,-26,0,26,80,105})foreach(float z in new[]{-40f,0,28,55,85})foreach(float y in new[]{2f,15,30,43})points.Add(new Vector3(x,y,z));
            group.probePositions=points.ToArray();
            foreach(var l in Obj.FindObjectsByType<Light>(FindObjectsSortMode.None))if(l.type!=LightType.Directional){l.lightmapBakeType=LightmapBakeType.Mixed;l.bounceIntensity=.6f;}
            File.WriteAllText(WideOutput+"/lighting-preparation.txt","Z1 bake renderers: "+marked+"\nDistinct lightmap meshes: "+used.Count+"\nLight probes: "+points.Count+"\nResolution: 3 texels/metre, 1024 atlas, CPU progressive, indirect-only mixed light.\nB01/B02/B03, fixed I1 equipment and adjacent small ground/apron surfaces. Other zones use ambient, installed task lighting and local reflections.\n");
        }
        static void BakeLocalReflections()
        {
            foreach(var probe in Obj.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))Obj.DestroyImmediate(probe.gameObject);
            var points=new[]{new Vector3(-95,14,45),new Vector3(0,22,-26),new Vector3(0,24,45),new Vector3(365,20,480),new Vector3(-125,26,295),new Vector3(90,42,555),new Vector3(100,30,715)};
            var sizes=new[]{new Vector3(65,43,57),new Vector3(64,50,65),new Vector3(150,70,150),new Vector3(240,100,350),new Vector3(170,70,210),new Vector3(190,100,180),new Vector3(170,110,160)};
            var report=new StringBuilder("probe,file,success\n");
            labels.gameObject.SetActive(false);routes.gameObject.SetActive(false);proxies.gameObject.SetActive(false);volumes.gameObject.SetActive(false);
            for(int i=0;i<points.Length;i++)
            {
                var probe=new GameObject("Local reflection "+i).AddComponent<ReflectionProbe>();probe.transform.position=points[i];
                probe.mode=ReflectionProbeMode.Baked;probe.resolution=128;probe.hdr=true;probe.size=sizes[i];probe.boxProjection=true;probe.blendDistance=8;
                probe.nearClipPlane=.3f;probe.farClipPlane=2000;probe.clearFlags=ReflectionProbeClearFlags.Skybox;probe.intensity=.85f;
                probe.cullingMask=~((1<<8)|(1<<9)|(1<<10)|(1<<11)|(1<<12));
                string path=LightingRoot+"LocalReflection_"+i+".exr";bool success=Lightmapping.BakeReflectionProbe(probe,path);
                if(!success)throw new InvalidOperationException("Local reflection bake failed "+i);
                AssetDatabase.ImportAsset(path);probe.bakedTexture=AssetDatabase.LoadAssetAtPath<Texture>(path);report.AppendLine(i+","+path+","+success);
            }
            proxies.gameObject.SetActive(true);File.WriteAllText(WideOutput+"/local-reflections.csv",report.ToString());
        }
        static void WarmSceneParticles()
        {
            foreach(var ps in Obj.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.Simulate(ps.main.duration*.85f,true,true,true);ps.Pause(true);}
        }
        [MenuItem("Anseong/Map35/Bake Z1 Lighting And Build Visual Upgrade")]
        public static void BakeVisualUpgradeAndBuild()
        {
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();
            PlayerSettings.SetStaticBatchingForPlatform(BuildTarget.StandaloneWindows64,true);PlayerSettings.SetDynamicBatchingForPlatform(BuildTarget.StandaloneWindows64,false);
            AlignAcquiredGroundContact();
            ValidateFiniteMapGeometry();
            if(GameObject.Find("B02 maintenance service yard")!=null)ValidateMaintenanceAccess();
            PrepareZ1Lighting();AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
            Debug.Log("Z1_INDIRECT_BAKE_START");bool baked=Lightmapping.Bake();
            if(!baked||LightmapSettings.lightmaps.Length==0)throw new InvalidOperationException("Z1 indirect lighting produced no lightmaps");
            Debug.Log("Z1_INDIRECT_BAKE_OK maps="+LightmapSettings.lightmaps.Length);
            WarmSceneParticles();BakeLocalReflections();AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);ValidateWide();Capture(WideOutput);AuditVisualUpgrade();
            if(viewer.views.Any(c=>c.name.Contains("painted and corroded cladding")))CaptureLocalWearComparison();
            viewer.Select(0);viewer.labels.SetActive(true);viewer.routes.SetActive(true);viewer.frusta.SetActive(false);viewer.reservations.SetActive(false);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{WideScene},locationPathName="../Builds/MapWide/AnseongMapWide.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Visual upgrade Windows build failed");
            Debug.Log("WIDE_VISUAL_UPGRADE_BUILD_OK");
        }
        [MenuItem("Anseong/Map35/Finalize Surface Review And Build")]
        public static void FinalizeSurfaceReviewAndBuild()
        {
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();
            FreePbrMaterials();RefineSurfaceScale();RefreshLandscapeDistanceLook();
            assetRoots.Clear();
            foreach(string id in new[]{"B01","B02","B03","B04","B05","B06","B07","B08","B09"})
            {
                var root=Existing(id);if(PrefabUtility.IsPartOfPrefabInstance(root))PrefabUtility.UnpackPrefabInstance(root.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                var lod=root.GetComponent<LODGroup>();if(lod!=null)Obj.DestroyImmediate(lod);
                var far=root.Find("LOD1 combined structure");if(far!=null)Obj.DestroyImmediate(far.gameObject);assetRoots.Add(root);
            }
            SaveBuildingAssets();cameraList.Clear();cameraList.AddRange(viewer.views);
            cameras=GameObject.Find("08 Review cameras").transform;
            if(viewer.views.Length==35)
            {
                MakeCamera("DETAIL PC drone / maintenance equipment",new Vector3(-69,2.2f,30),new Vector3(-74,.9f,35),60);
                MakeCamera("DETAIL PC drone / natural shoreline",new Vector3(430,6,801),new Vector3(454,0,812),60);
                viewer.views=cameraList.ToArray();
            }
            ConfigureProductionLook();PlayerSettings.SetStaticBatchingForPlatform(BuildTarget.StandaloneWindows64,true);PlayerSettings.SetDynamicBatchingForPlatform(BuildTarget.StandaloneWindows64,false);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
            BakeVisualUpgradeAndBuild();
        }
        static void ReconnectWideScene()
        {
            viewer=Obj.FindFirstObjectByType<Map35Viewer>();labels=viewer.labels.transform;routes=viewer.routes.transform;volumes=viewer.reservations.transform;
            structures=GameObject.Find("01 Buildings and traversable ground").transform;
            var all=Obj.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            proxies=all.First(t=>t.name=="04 Scale proxies - phase snapshots");
            details=all.First(t=>t.name=="02 Interaction markers and scale details");
            nature=all.First(t=>t.name=="03 Landscape and river");
            cameras=all.First(t=>t.name=="08 Review cameras");
            districts=all.FirstOrDefault(t=>t.name=="10 Reused industrial districts");
            nearDetails=all.FirstOrDefault(t=>t.name=="11 Nine-sector close details");
            ruins=all.FirstOrDefault(t=>t.name=="12 Invasion damage and wreckage");
            // Recreate only the target lookup used by validation; no scene geometry is changed here.
            targets.Clear();targets["I1 push"]=new Vector3(0,24,7.3f);targets["I1 status"]=new Vector3(-5.4f,26,8);targets["I1 latch"]=new Vector3(8,26,8);
            targets["T1 shell"]=new Vector3(-12,32,270);targets["T2 nerve"]=new Vector3(-20,34,283);targets["I6 neck"]=new Vector3(90,24,512);targets["Coil edge"]=new Vector3(96.4f,52,555);targets["P02 latch"]=new Vector3(140,48,555);
        }
        static void AuditVisualUpgrade()
        {
            var renderers=Obj.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var missing=renderers.Where(r=>r.sharedMaterials.Any(m=>m==null||m.shader==null||m.shader.name=="Hidden/InternalErrorShader")).Select(r=>r.name).ToArray();
            if(missing.Length>0)throw new InvalidOperationException("Missing materials/shaders: "+string.Join(",",missing));
            var report=new StringBuilder();report.AppendLine("Mesh renderers including LODs/inactive: "+renderers.Length);report.AppendLine("Missing materials or error shaders: "+missing.Length);
            report.AppendLine("Lightmap atlases: "+LightmapSettings.lightmaps.Length);report.AppendLine("Baked light probes: "+(LightmapSettings.lightProbes==null?0:LightmapSettings.lightProbes.count));
            report.AppendLine("Local reflection probes: "+Obj.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Length);
            report.AppendLine("Post processing layers: "+Obj.FindObjectsByType<PostProcessLayer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
            report.AppendLine("This is a desktop map viewer art check, not an HMD frame-time or full gameplay acceptance test.");File.WriteAllText(WideOutput+"/visual-upgrade-audit.txt",report.ToString());
        }
    }
}
