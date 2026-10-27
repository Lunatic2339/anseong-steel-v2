using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace CoastalFactory.Editor {
public static class WarehouseAssetOrganizer {
 const string Old="Assets/_Anseong/Art/Environment/DestructibleFactory";
 const string Target="Assets/_Anseong/Art/Environment/Assets/Warehouse";
 [MenuItem("Anseong/Art/Organize Warehouse Asset")]
 public static void Organize(){
  AssetDatabase.Refresh();
  foreach(var name in new[]{"Generated","Runtime","Editor","README.txt"}){
   string from=Old+"/"+name,to=Target+"/"+name;
   if(!AssetDatabase.IsValidFolder(from)&&!File.Exists(from))continue;
   string error=AssetDatabase.MoveAsset(from,to);if(error.Length>0)throw new Exception(error);
  }
  string previous=Target+"/Generated/SteelworksFactory.prefab",prefab=Target+"/Warehouse.prefab";
  if(File.Exists(previous)){string error=AssetDatabase.MoveAsset(previous,prefab);if(error.Length>0)throw new Exception(error);}
  var contents=PrefabUtility.LoadPrefabContents(prefab);
  try{contents.name="Warehouse";PrefabUtility.SaveAsPrefabAsset(contents,prefab);}finally{PrefabUtility.UnloadPrefabContents(contents);}
  AssetDatabase.SaveAssets();AssetDatabase.Refresh();
  var asset=AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
  if(asset==null||asset.name!="Warehouse")throw new Exception("Warehouse prefab validation failed");
  foreach(var scene in new[]{"Sandbox_FactoryDemo","Sandbox_RobotFall"}){
   string path="Assets/_Anseong/Scenes/Sandbox/"+scene+".unity";
   if(Array.IndexOf(AssetDatabase.GetDependencies(path,true),prefab)<0)throw new Exception("Missing prefab reference: "+scene);
  }
  Debug.Log("WAREHOUSE_ORGANIZE_PASS prefab="+prefab+" pieces="+asset.GetComponentsInChildren<FactoryPiece>(true).Length);
 }
}
}
