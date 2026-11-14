using System.IO;
using Anseong.Z1;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace Anseong.Z1.Editor
{
    public static class Z1Build
    {
        [MenuItem("Anseong/Z1/Capture Layout Previews")]
        public static void Capture()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameObject("Preview").AddComponent<Z1World>();world.Build();
            var state=new PlateState();
            Directory.CreateDirectory("../docs/validation/z1");
            for(int i=0;i<3;i++)
            {
                world.Show(state,i==0?Seat.Mech:Seat.Support,i==2?Variant.Control:Variant.Drone);
                var position=i==0?new Vector3(0,29.75f,0):i==1?state.drone:PlateNetwork.ControlPosition(1);
                var target=i==0?new Vector3(0,24,8):PlateNetwork.Target;
                world.view.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
                var rt=new RenderTexture(1280,800,24);world.view.targetTexture=rt;world.view.Render();
                var previous=RenderTexture.active;RenderTexture.active=rt;
                var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();
                File.WriteAllBytes("../docs/validation/z1/"+new[]{"mech","drone","control"}[i]+".png",image.EncodeToPNG());
                RenderTexture.active=previous;world.view.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);
            }
            Debug.Log("Z1_CAPTURE_OK");
        }
        public const string ScenePath="Assets/Z1/Scenes/Z1Coop.unity";
        [MenuItem("Anseong/Z1/Prepare Scene and XR")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/Z1/Scenes"); Directory.CreateDirectory("Assets/Z1/Settings");
            Directory.CreateDirectory("Assets/Z1/Resources");
            if(AssetDatabase.LoadAssetAtPath<Material>("Assets/Z1/Resources/Z1Base.mat")==null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")),"Assets/Z1/Resources/Z1Base.mat");
            PlayerSettings.companyName="Anseong"; PlayerSettings.productName="Anseong Z1 Coop Lab";
            PlayerSettings.runInBackground=true; PlayerSettings.defaultIsNativeResolution=false;
            PlayerSettings.defaultScreenWidth=1280; PlayerSettings.defaultScreenHeight=800;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            settings.FindProperty("activeInputHandler").intValue=1;settings.ApplyModifiedPropertiesWithoutUndo();
            PrepareXR();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Z1 Prototype").AddComponent<Z1Prototype>();
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("Z1_PREPARE_OK");
        }
        // Shared project setup; does not regenerate the Z1 scene or change the active build scene.
        public static void PrepareXR()
        {
            Directory.CreateDirectory("Assets/Z1/Settings");
            if(!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey,out XRGeneralSettingsPerBuildTarget targets))
            {
                targets=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(targets,"Assets/Z1/Settings/XRTargets.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey,targets,true);
            }
            // General settings must be a sub-asset of the per-target container. A standalone
            // XRGeneral main asset is auto-migrated on reopen and can lose its manager reference.
            if(!targets.HasSettingsForBuildTarget(BuildTargetGroup.Standalone))
                targets.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if(!targets.HasManagerSettingsForBuildTarget(BuildTargetGroup.Standalone))
                targets.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Standalone);
            var general=targets.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            // PC support must never auto-start XR. Only the explicit VR role initializes it.
            general.InitManagerOnStart=false;
            general.Manager.automaticLoading=false;general.Manager.automaticRunning=false;
            XRPackageMetadataStore.AssignLoader(general.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",BuildTargetGroup.Standalone);
            var xr=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            if(xr!=null)
            {
                foreach(var feature in xr.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>())
                    if(feature.GetType().Name.Contains("OculusTouchControllerProfile"))feature.enabled=true;
                EditorUtility.SetDirty(xr);
            }
            EditorUtility.SetDirty(general);EditorUtility.SetDirty(general.Manager);EditorUtility.SetDirty(targets);
            // Preserve the older container too, replacing its stale main-asset reference after migration.
            var olderTargets=AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>("Assets/Z1/Settings/XRTargets.asset");
            if(olderTargets!=null && olderTargets!=targets)
                olderTargets.SetSettingsForBuildTarget(BuildTargetGroup.Standalone,general);
            AssetDatabase.SaveAssets();
            Debug.Log("XR_SETTINGS_READY manual-start manager="+(general.Manager!=null));
        }
        [MenuItem("Anseong/Z1/Build Windows Lab")]
        public static void Build()
        {
            Prepare();
            Directory.CreateDirectory("../Builds/Z1");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="../Builds/Z1/AnseongZ1.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new System.Exception("Z1 build failed: "+report.summary.result);
            Debug.Log("Z1_BUILD_OK");
        }
    }
}
