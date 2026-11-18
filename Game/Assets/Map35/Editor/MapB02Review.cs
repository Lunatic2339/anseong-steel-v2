using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Obj=UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        static string B02Output=>Path.GetFullPath("../docs/validation/b02-building-detail-2026-11-15");
        static readonly int[] B02ViewIndices={9,49,17,38,51,59,60,61};
        static string B02CameraSignature()=>string.Join("\n",viewer.views.Take(62).Select((c,i)=>i+"/"+c.name+"/"+c.transform.position.ToString("F5")+"/"+c.transform.rotation.ToString("F5")+"/"+c.fieldOfView.ToString("R",Inv)));
        static void B02Invoke(string name)
        {
            var method=typeof(Map35Build).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
            if(method==null)throw new InvalidOperationException("B02 stage unavailable: "+name);
            try{method.Invoke(null,null);}catch(TargetInvocationException e){throw e.InnerException??e;}
        }
        static List<Camera> B02ReviewCameras()
        {
            var result=B02ViewIndices.Select(i=>viewer.views[i]).ToList();
            string[] names={"B02 PC3 western panel joints","B02 PC3 eastern window depth"};
            Vector3[] pos={new Vector3(-138,10,35),new Vector3(-54,18,70)};
            Vector3[] target={new Vector3(-120,9,42),new Vector3(-71,16,56)};
            for(int i=0;i<2;i++)
            {
                var c=viewer.views.FirstOrDefault(v=>v.name==names[i]);
                if(c==null){c=Obj.Instantiate(viewer.views[38],cameras);c.name=names[i];c.enabled=false;c.tag="Untagged";c.transform.position=pos[i];c.transform.LookAt(target[i]);c.fieldOfView=55;c.targetTexture=null;}
                result.Add(c);
            }
            return result;
        }
        static void B02Capture(string phase)
        {
            var views=B02ReviewCameras();
            var states=new[]{viewer.scenario.gameObject,proxies.gameObject,viewer.labels,viewer.routes,viewer.frusta,viewer.reservations}.Distinct().Select(g=>(g,g.activeSelf)).ToArray();
            try
            {
                RepHideOverlays();
                var csv=new StringBuilder("file,viewer_index,name,x,y,z,forward_x,forward_y,forward_z,fov\n");
                for(int i=0;i<views.Count;i++)
                {
                    var c=views[i];float aspect=c.aspect;string file=phase+"-"+i.ToString("00")+".png";
                    try{SaveWearCamera(c,B02Output+"/"+file);}finally{c.aspect=aspect;}
                    csv.AppendLine(file+","+Array.IndexOf(viewer.views,c)+","+FacadeCsv(c.name)+","+RepVec(c.transform.position)+","+RepVec(c.transform.forward)+","+c.fieldOfView.ToString("R",Inv));
                }
                File.WriteAllText(B02Output+"/"+phase+"-cameras.csv",csv.ToString());
            }
            finally{foreach(var p in states)p.g.SetActive(p.activeSelf);}
        }
        static void B02Inventory(string phase)
        {
            var csv=new StringBuilder("path,enabled,active,vertices,triangles,uv0,uv2,uv3,colors,min_x,min_y,min_z,max_x,max_y,max_z,materials\n");
            foreach(var r in Existing("B02").GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf=r.GetComponent<MeshFilter>();if(mf==null)continue;var m=mf.sharedMesh;if(m==null)continue;
                csv.AppendLine(FacadeCsv(ScenarioValidationPath(r.transform))+","+r.enabled+","+r.gameObject.activeInHierarchy+","+m.vertexCount+","+(m.isReadable?m.triangles.Length/3:-1)+","+(m.isReadable?m.uv.Length:-1)+","+(m.isReadable?m.uv2.Length:-1)+","+(m.isReadable?m.uv3.Length:-1)+","+(m.isReadable?m.colors.Length:-1)+","+RepVec(r.bounds.min)+","+RepVec(r.bounds.max)+","+FacadeCsv(string.Join(";",r.sharedMaterials.Where(x=>x!=null).Select(AssetDatabase.GetAssetPath))));
            }
            File.WriteAllText(B02Output+"/"+phase+"-inventory.csv",csv.ToString());
        }
        public static void ReviewB02Building()
        {
            string phase=RepArg("-b02Phase","before");
            if(!new[]{"before","geometry","materials","weather-off","final"}.Contains(phase))throw new ArgumentException("Invalid B02 phase");
            Directory.CreateDirectory(B02Output);EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();
            string scene=AuditHash(WideScene),look=FacadeLookSignature(),anchors=PhotoAnchorSignature(),poses=B02CameraSignature();
            var originalMats=RepMaterialHashes();var colliders=RepColliderStates();
            if(phase=="before"&&!File.Exists(B02Output+"/v019-scene.unity.bak"))File.Copy(WideScene,B02Output+"/v019-scene.unity.bak");
            if(phase=="geometry"||phase=="materials"||phase=="weather-off")B02Invoke("BuildB02Architecture");
            if(phase=="materials"||phase=="weather-off")B02Invoke("ApplyB02PanelMaterials");
            if(phase=="geometry"||phase=="materials"||phase=="weather-off")B02RefreshLOD(phase);
            var weather=new List<(Material,float)>();
            if(phase=="weather-off")foreach(var m in Existing("B02").GetComponentsInChildren<MeshRenderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null&&m.HasProperty("_WeatherStrength")).Distinct())
            {weather.Add((m,m.GetFloat("_WeatherStrength")));m.SetFloat("_WeatherStrength",0);}
            try{B02Inventory(phase);B02Capture(phase);}finally{foreach(var p in weather)p.Item1.SetFloat("_WeatherStrength",p.Item2);}
            if(look!=FacadeLookSignature()||anchors!=PhotoAnchorSignature()||poses!=B02CameraSignature())throw new InvalidOperationException("B02 protected scene values changed");
            foreach(var p in originalMats)if(AuditHash(p.Key)!=p.Value)throw new InvalidOperationException("B02 changed original material "+p.Key);
            foreach(var p in colliders)if(p.Key==null||RepColliderState(p.Key)!=p.Value)throw new InvalidOperationException("B02 changed original collider");
            ValidateMaintenanceAccess();ValidateFiniteMapGeometry();if(phase!="before")B02ValidateNewMeshChannels(phase);
            if(scene!=AuditHash(WideScene))throw new InvalidOperationException("B02 review saved production scene");
            File.WriteAllText(B02Output+"/"+phase+"-integrity.txt","Scene SHA256="+scene+"\nOriginal material files="+originalMats.Count+"\nOriginal collider components="+colliders.Count+"\nGlobal look/anchors/62 original cameras/source materials/collider components preserved=True\nScene saved=False\n");
            Debug.Log("B02_REVIEW_OK "+phase);
        }
        static void B02RefreshLOD(string phase)
        {
            var root=Existing("B02");var group=root.GetComponent<LODGroup>();if(group==null)throw new InvalidOperationException("B02 LOD missing");
            var levels=group.GetLODs();if(levels.Length!=2)throw new InvalidOperationException("B02 LOD layout changed");
            var oldFar=root.Find("LOD1 combined structure");if(oldFar==null)throw new InvalidOperationException("B02 old LOD missing");
            var oldNear=levels[0].renderers;
            var near=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.gameObject.activeInHierarchy&&r.enabled&&!r.transform.IsChildOf(oldFar)&&r.GetComponent<MeshFilter>()!=null&&r.GetComponent<TextMesh>()==null).ToArray();
            var buckets=new Dictionary<(Material mat,UnityEngine.Rendering.ShadowCastingMode shadow,bool receive),List<CombineInstance>>();
            foreach(var r in near)
            {
                if(r.name.IndexOf("fastener",StringComparison.OrdinalIgnoreCase)>=0||r.name.StartsWith("B02 panel fixing ",StringComparison.Ordinal))continue;
                var mf=r.GetComponent<MeshFilter>();if(mf.sharedMesh==null)continue;
                for(int s=0;s<mf.sharedMesh.subMeshCount&&s<r.sharedMaterials.Length;s++)
                {
                    var m=r.sharedMaterials[s];if(m==null)continue;
                    var key=(m,r.shadowCastingMode,r.receiveShadows);
                    if(!buckets.TryGetValue(key,out var list)){list=new List<CombineInstance>();buckets.Add(key,list);}
                    list.Add(new CombineInstance{mesh=mf.sharedMesh,subMeshIndex=s,transform=root.worldToLocalMatrix*mf.transform.localToWorldMatrix});
                }
            }
            string folder="Assets/Map35/B02Architecture/LOD/"+phase;PurposeFolder(folder);
            var parent=new GameObject("B02 updated LOD").transform;parent.SetParent(root,false);var far=new List<Renderer>();
            long triangles=0;
            foreach(var pair in buckets.OrderBy(p=>AssetDatabase.GetAssetPath(p.Key.mat)))
            {
                var mesh=new Mesh{name="B02 material LOD",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();
                string path=folder+"/"+Hash128.Compute(AssetDatabase.GetAssetPath(pair.Key.mat)+"/"+pair.Key.shadow+"/"+pair.Key.receive).ToString()+".asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);Obj.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
                var go=new GameObject("B02 LOD "+pair.Key.mat.name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=saved;
                var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=pair.Key.mat;r.shadowCastingMode=pair.Key.shadow;r.receiveShadows=pair.Key.receive;r.receiveGI=ReceiveGI.LightProbes;far.Add(r);triangles+=saved.triangles.Length/3;
            }
            oldFar.gameObject.SetActive(false);oldFar.name="B02 original LOD preserved";parent.name="LOD1 combined structure";
            levels[0].renderers=near.Concat(oldNear.Where(r=>r!=null&&r.GetComponent<TextMesh>()!=null)).Distinct().ToArray();levels[1].renderers=far.ToArray();group.SetLODs(levels);group.RecalculateBounds();
            File.WriteAllText(B02Output+"/"+phase+"-lod.txt","Near renderers="+near.Length+"\nFar material groups="+far.Count+"\nFar triangles="+triangles+"\nOriginal LOD retained inactive; updated transforms baked building-local. Transition values="+levels[0].screenRelativeTransitionHeight+","+levels[1].screenRelativeTransitionHeight+"\nNo HMD performance acceptance.\n");
        }
        public static void CommitB02Building()
        {
            Directory.CreateDirectory(B02Output);EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();
            if(Existing("B02").Find("B02 original LOD preserved")!=null)throw new InvalidOperationException("B02 already committed");
            string look=FacadeLookSignature(),anchors=PhotoAnchorSignature(),poses=B02CameraSignature();var mats=RepMaterialHashes();var colliders=RepColliderStates();
            B02Invoke("BuildB02Architecture");B02Invoke("ApplyB02PanelMaterials");B02RefreshLOD("committed");
            var extra=B02ReviewCameras().Skip(B02ViewIndices.Length);viewer.views=viewer.views.Concat(extra.Where(c=>!viewer.views.Contains(c))).ToArray();EditorUtility.SetDirty(viewer);
            if(look!=FacadeLookSignature()||anchors!=PhotoAnchorSignature()||poses!=B02CameraSignature())throw new InvalidOperationException("B02 protected state changed");
            foreach(var p in mats)if(AuditHash(p.Key)!=p.Value)throw new InvalidOperationException("Original material changed "+p.Key);
            foreach(var p in colliders)if(p.Key==null||RepColliderState(p.Key)!=p.Value)throw new InvalidOperationException("Original collider component changed");
            ValidateMaintenanceAccess();ValidateFiniteMapGeometry();B02ValidateNewMeshChannels("committed");RepValidateConnections();ValidateScenarioSpatialChecks(viewer.scenario);
            if(look!=FacadeLookSignature()||anchors!=PhotoAnchorSignature()||poses!=B02CameraSignature())throw new InvalidOperationException("B02 validation altered protected state");
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
            B02Inventory("committed");B02ExportPrefab();File.WriteAllText(B02Output+"/commit.txt","Scene="+AuditHash(WideScene)+"\nOriginal materials/collider components/global look/anchors/62 cameras preserved=True\n");Debug.Log("B02_COMMIT_OK");
        }
        static void B02ValidateNewMeshChannels(string phase)
        {
            var root=Existing("B02").Find("B02 architectural depth upgrade");if(root==null)throw new InvalidOperationException("New B02 architecture missing");
            int meshCount=0,vertices=0,degenerate=0;var problems=new List<string>();
            Func<float,bool> finite=x=>!float.IsNaN(x)&&!float.IsInfinity(x);
            foreach(var m in root.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).Distinct())
            {
                meshCount++;vertices+=m.vertexCount;var p=m.vertices;var n=m.normals;var t=m.tangents;var uv=new List<Vector4>();m.GetUVs(2,uv);
                if(n.Length!=p.Length||t.Length!=p.Length||m.uv2.Length!=p.Length||uv.Count!=p.Length||m.colors.Length!=p.Length){problems.Add(m.name+" missing channels");continue;}
                for(int i=0;i<p.Length;i++)if(!finite(n[i].x)||!finite(n[i].y)||!finite(n[i].z)||!finite(t[i].x)||!finite(t[i].y)||!finite(t[i].z)||!finite(t[i].w))problems.Add(m.name+" nonfinite shading channel");
                var ix=m.triangles;for(int i=0;i<ix.Length;i+=3)if(Vector3.Cross(p[ix[i+1]]-p[ix[i]],p[ix[i+2]]-p[ix[i]]).sqrMagnitude<1e-16f)degenerate++;
            }
            File.WriteAllText(B02Output+"/"+phase+"-mesh-channels.txt","Meshes="+meshCount+"\nVertices="+vertices+"\nDegenerate triangles="+degenerate+"\nChannel errors="+problems.Count+"\n"+string.Join("\n",problems));
            if(problems.Count>0||degenerate>0)throw new InvalidOperationException("B02 mesh channel/degenerate check failed: "+string.Join(";",problems.Take(4))+" degenerate="+degenerate);
        }
        static void B02ExportPrefab()
        {
            string path="Assets/Map35/B02Architecture/B02_DetailedMaintenance.prefab";var copy=Obj.Instantiate(Existing("B02").gameObject);copy.name="B02 Detailed Maintenance";
            try
            {
                if(PrefabUtility.IsPartOfPrefabInstance(copy))PrefabUtility.UnpackPrefabInstance(copy,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                copy.transform.position=Vector3.zero;
                foreach(var r in copy.GetComponentsInChildren<MeshRenderer>(true)){r.lightmapIndex=-1;r.lightmapScaleOffset=new Vector4(1,1,0,0);r.receiveGI=ReceiveGI.LightProbes;}
                if(PrefabUtility.SaveAsPrefabAsset(copy,path)==null)throw new InvalidOperationException("B02 prefab export failed");
                File.WriteAllText(B02Output+"/prefab.txt",path+"\nCurrent building geometry/materials/LOD/colliders; root placed at origin; scene lightmap indices cleared for reuse. Original prefab untouched.\n");
            }
            finally{Obj.DestroyImmediate(copy);}
        }
        public static void BakeBuildB02Building()
        {
            Directory.CreateDirectory(B02Output);EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();
            if(Existing("B02").Find("B02 original LOD preserved")==null)throw new InvalidOperationException("Commit B02 first");
            string look=FacadeLookSignature(),anchors=PhotoAnchorSignature(),poses=B02CameraSignature();
            PrepareZ1Lighting();AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
            if(!Lightmapping.Bake()||LightmapSettings.lightmaps.Length==0)throw new InvalidOperationException("B02 GI bake failed");
            WarmSceneParticles();DynamicGI.UpdateEnvironment();var states=new[]{viewer.labels,viewer.routes,proxies.gameObject,viewer.reservations}.Distinct().Select(g=>(g,g.activeSelf)).ToArray();
            try{BakeLocalReflections();}finally{foreach(var p in states)p.g.SetActive(p.activeSelf);}
            if(look!=FacadeLookSignature()||anchors!=PhotoAnchorSignature()||poses!=B02CameraSignature())throw new InvalidOperationException("B02 bake protected state changed");
            ValidateMaintenanceAccess();ValidateFiniteMapGeometry();RepValidateConnections();ValidateScenarioSpatialChecks(viewer.scenario);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
            FinalizeB02Building();
        }
        public static void FinalizeB02Building()
        {
            Directory.CreateDirectory(B02Output);EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();string hash=AuditHash(WideScene);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{WideScene},locationPathName="../Builds/MapWide/AnseongMapWide.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("B02 Windows build failed");
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();B02Capture("final");B02Inventory("final");
            if(hash!=AuditHash(WideScene))throw new InvalidOperationException("B02 final capture saved scene");
            File.WriteAllText(B02Output+"/build-validation.txt","Build="+result.summary.result+"\nScene SHA256="+hash+"\nLightmaps="+LightmapSettings.lightmaps.Length+"\nCameras="+viewer.views.Length+"\n");Debug.Log("B02_BUILD_OK");
        }
    }
}
