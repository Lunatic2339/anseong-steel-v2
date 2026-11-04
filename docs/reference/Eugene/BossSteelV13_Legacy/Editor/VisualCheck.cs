using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;
using System.IO;
namespace BossSteelV13.Editor {
 public static class VisualCheck {
  public static void Run(){
   EditorSceneManager.OpenScene(SteelV13Installer.ScenePath);
   var camera=Camera.main;var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;data.antialiasingQuality=AntialiasingQuality.High;
   EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
   camera.fieldOfView=30;
   var m=AssetDatabase.LoadAssetAtPath<Material>(SteelV13Installer.Root+"/SteelV13.mat");
   var old=m.GetFloat("_BumpScale");
   m.SetFloat("_BumpScale",0);SteelV13Installer.Capture(camera,"Library/BossSteelV13/normal-off.png");
   m.SetFloat("_BumpScale",old);SteelV13Installer.Capture(camera,"Library/BossSteelV13/normal-on.png");
   File.WriteAllText("Library/BossSteelV13/texture-settings.txt", "Normal: "+m.GetTexture("_BumpMap").width+" x "+m.GetTexture("_BumpMap").height);
  }
 }
}
