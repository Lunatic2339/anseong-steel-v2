using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Obj = UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        static string RepOutput => Path.GetFullPath("../docs/validation/map-representative-section-2026-11-15");
        static string RepArg(string name, string fallback) { var a=Environment.GetCommandLineArgs(); int i=Array.IndexOf(a,name);return i>=0&&i+1<a.Length?a[i+1]:fallback; }
        static List<Camera> RepCameras()
        {
            var list=new List<Camera>{viewer.views[9],viewer.views[49],viewer.views[17],viewer.views[38]};
            string[] names={"REP PC3 approach","REP PC3 loading edge","REP PC4 detail"};
            Vector3[] positions={new Vector3(-34,18,4),new Vector3(-50,3.6f,39),new Vector3(-95,80,95)};
            Vector3[] looks={new Vector3(-54,2,37),new Vector3(-64,.7f,49),new Vector3(-54,2,37)};
            for(int i=0;i<names.Length;i++)
            {
                var existing=viewer.views.FirstOrDefault(c=>c.name==names[i]);
                if(existing!=null){list.Add(existing);continue;}
                var c=Obj.Instantiate(viewer.views[17],cameras);c.name=names[i];c.enabled=false;
                c.transform.position=positions[i];c.transform.LookAt(looks[i]);c.fieldOfView=i==0?75:i==1?60:32;c.aspect=1.6f;list.Add(c);
            }
            return list;
        }
        static void RepHideOverlays()
        {
            WarmSceneParticles();
            foreach(var ps in Obj.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include,FindObjectsSortMode.None)) ps.Pause(true);
            foreach(var go in new[]{viewer.scenario.gameObject,proxies.gameObject,viewer.labels,viewer.routes,viewer.frusta,viewer.reservations}.Distinct())go.SetActive(false);
        }
        public static void CompareRepresentativeSection()
        {
            string phase=RepArg("-representativePhase","");
            if(!new[]{"before","before-repeat","layout","depth"}.Contains(phase))throw new ArgumentException("Separate fresh process per representative phase required");
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();Directory.CreateDirectory(RepOutput);
            if(structures.Find("REP depth connections")!=null)throw new InvalidOperationException("Comparison must use the unchanged v0.17 scene");
            string sceneHash=AuditHash(WideScene),look=FacadeLookSignature(),anchors=PhotoAnchorSignature(),cameraSignature=RepOriginalCameras();
            var materialHashes=RepMaterialHashes();var originalColliders=RepColliderStates();
            RepHideOverlays();var views=RepCameras();
            if(phase=="layout"||phase=="depth") RepApplyLayout();
            if(phase=="depth") RepApplyDepth();
            RepCheckProtected(look,anchors,cameraSignature,materialHashes,originalColliders);
            Physics.SyncTransforms();ValidateMaintenanceAccess();ValidateFiniteMapGeometry();
            RepVisibility(views,phase);RepCapture(views,phase);
            AssetDatabase.SaveAssets();RepCheckProtected(look,anchors,cameraSignature,materialHashes,originalColliders);
            if(sceneHash!=AuditHash(WideScene))throw new InvalidOperationException("Comparison saved production scene unexpectedly");
            File.WriteAllText(RepOutput+"/"+phase+"-integrity.txt","Scene SHA256="+sceneHash+"\nOriginal material files="+materialHashes.Count+"\nOriginal collider components="+originalColliders.Count+"\nOriginal materials/colliders/global look/anchors/59 cameras preserved=True\nScene saved=False\nNew mesh assets only; current baked lighting retained for single-stage comparison.\n");
            Debug.Log("REPRESENTATIVE_COMPARISON_OK "+phase);
        }
        static string RepOriginalCameras()=>string.Join("\n",viewer.views.Take(59).Select((c,i)=>i+"/"+c.name+"/"+c.transform.position.ToString("F5")+"/"+c.transform.rotation.ToString("F5")+"/"+c.fieldOfView.ToString("R",Inv)));
        static Dictionary<string,string> RepMaterialHashes()=>Obj.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Select(AssetDatabase.GetAssetPath).Where(File.Exists).Distinct().ToDictionary(p=>p,AuditHash);
        static string RepColliderState(Collider c)=>c.enabled+"/"+c.isTrigger+"/"+c.transform.localToWorldMatrix.ToString("F5")+"/"+EditorJsonUtility.ToJson(c);
        static Dictionary<Collider,string> RepColliderStates()=>Obj.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None).ToDictionary(c=>c,RepColliderState);
        static void RepCheckProtected(string look,string anchors,string poses,Dictionary<string,string> mats,Dictionary<Collider,string> colliders)
        {
            if(look!=FacadeLookSignature()||anchors!=PhotoAnchorSignature()||poses!=RepOriginalCameras())throw new InvalidOperationException("Protected look/anchor/camera changed");
            foreach(var p in mats)if(AuditHash(p.Key)!=p.Value)throw new InvalidOperationException("Material changed "+p.Key);
            foreach(var p in colliders)if(p.Key==null||RepColliderState(p.Key)!=p.Value)throw new InvalidOperationException("Existing collider changed "+(p.Key==null?"destroyed":ScenarioValidationPath(p.Key.transform)));
        }
        public static void CommitRepresentativeSection()
        {
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();Directory.CreateDirectory(RepOutput);
            if(structures.Find("REP depth connections")!=null)throw new InvalidOperationException("Section already committed; do not overwrite original baseline");
            string backup=RepOutput+"/v017-scene.unity.bak";
            if(!File.Exists(backup))File.Copy(WideScene,backup);
            string look=FacadeLookSignature(),anchors=PhotoAnchorSignature(),poses=RepOriginalCameras();
            var materials=RepMaterialHashes();var colliders=RepColliderStates();
            RepApplyLayout();RepApplyDepth();
            var views=RepCameras();viewer.views=viewer.views.Concat(views.Skip(4).Where(c=>!viewer.views.Contains(c))).ToArray();EditorUtility.SetDirty(viewer);
            RepCheckProtected(look,anchors,poses,materials,colliders);
            ValidateMaintenanceAccess();ValidateFiniteMapGeometry();RepValidateConnections();
            ValidateScenarioSpatialChecks(viewer.scenario);
            RepCheckProtected(look,anchors,poses,materials,colliders);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
            File.WriteAllText(RepOutput+"/committed.txt","Saved local layout + dock/drain crossing. Existing material files, 793 collider components, global look, scenario anchors and59 original cameras preserved. Added3 inspection cameras.\nScene SHA256="+AuditHash(WideScene)+"\n");
            Debug.Log("REPRESENTATIVE_COMMIT_OK");
        }
        static void RepValidateConnections()
        {
            Physics.SyncTransforms();var rows=new StringBuilder("x,z,expected_y,hit_y,collider\n");int count=0;
            foreach(float z in new[]{47.5f,49.1f,50.7f})
            for(float x=-70.4f;x<=-53.8f;x+=.2f)
            {
                float expected=x<=-62?1:x>=-54?0:1-(x+62)/8;
                if(x>=-62.45f&&x<=-61.55f)expected=1.025f-.0625f*(x+62.45f);
                if(!Physics.Raycast(new Vector3(x,2,z),Vector3.down,out var hit,3))throw new InvalidOperationException("Loading route support missing "+x+","+z);
                if(Mathf.Abs(hit.point.y-expected)>.035f)throw new InvalidOperationException("Loading route height mismatch "+x+" actual="+hit.point.y+" expected="+expected);
                rows.AppendLine(x.ToString("R",Inv)+","+z.ToString("R",Inv)+","+expected.ToString("R",Inv)+","+hit.point.y.ToString("R",Inv)+","+FacadeCsv(hit.collider.name));count++;
            }
            var own=structures.Find("REP depth connections");var ownColliders=own.GetComponentsInChildren<Collider>();
            int drainageSamples=0;
            foreach(float x in new[]{-62.2f,-62f,-61.8f})foreach(float y in new[]{.2f,.55f})
            {
            foreach(var c in ownColliders)
                if(c.Raycast(new Ray(new Vector3(x,y,46.8f),Vector3.forward),out var hit,4.6f))throw new InvalidOperationException("New construction blocks existing drainage line: "+c.name);
            drainageSamples++;
            }
            File.WriteAllText(RepOutput+"/loading-route-support.csv",rows.ToString());
            File.WriteAllText(RepOutput+"/connection-validation.txt","Support samples="+count+"\nAdded colliders="+ownColliders.Length+"\nDrainage passage lines="+drainageSamples+" all clear of new colliders=True\nNo real drainage fluid simulation. Existing pedestrian access tested separately; no new player walking simulation.\n");
        }
        public static void BakeBuildRepresentativeSection()
        {
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();Directory.CreateDirectory(RepOutput);
            if(structures.Find("REP depth connections")==null)throw new InvalidOperationException("Commit section first");
            string look=FacadeLookSignature(),anchors=PhotoAnchorSignature(),poses=RepOriginalCameras();
            var mats=RepMaterialHashes();var colliders=RepColliderStates();
            PrepareZ1Lighting();AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
            if(!Lightmapping.Bake()||LightmapSettings.lightmaps.Length==0)throw new InvalidOperationException("Representative section GI bake failed");
            WarmSceneParticles();DynamicGI.UpdateEnvironment();
            var states=new[]{viewer.labels,viewer.routes,proxies.gameObject,viewer.reservations}.Select(g=>(g,g.activeSelf)).ToArray();
            try{BakeLocalReflections();}finally{foreach(var p in states)p.g.SetActive(p.activeSelf);}
            RepCheckProtected(look,anchors,poses,mats,colliders);RepValidateConnections();ValidateMaintenanceAccess();ValidateFiniteMapGeometry();ValidateScenarioSpatialChecks(viewer.scenario);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
            FinalizeRepresentativeSection();
        }
        public static void FinalizeRepresentativeSection()
        {
            // Also supports resuming after a post-build capture failure without rebaking completed GI.
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();Directory.CreateDirectory(RepOutput);
            if(structures.Find("REP depth connections")==null||viewer.views.Length!=62||LightmapSettings.lightmaps.Length==0)
                throw new InvalidOperationException("Committed, baked representative scene required");
            string savedHash=AuditHash(WideScene);
            RepValidateConnections();ValidateMaintenanceAccess();ValidateFiniteMapGeometry();ValidateScenarioSpatialChecks(viewer.scenario);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{WideScene},locationPathName="../Builds/MapWide/AnseongMapWide.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Representative section Windows build failed");
            // BuildPipeline reloads the scene; static references now point at destroyed components.
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();
            RepHideOverlays();RepCapture(RepCameras(),"final");
            if(savedHash!=AuditHash(WideScene))throw new InvalidOperationException("Final inspection unexpectedly changed saved scene");
            File.WriteAllText(RepOutput+"/build-validation.txt","Build="+result.summary.result+"\nLightmaps="+LightmapSettings.lightmaps.Length+"\nCameras="+viewer.views.Length+"\nScene SHA256="+AuditHash(WideScene)+"\nBuild SHA256="+AuditHash("../Builds/MapWide/AnseongMapWide.exe")+"\nGlobal look unchanged; GI/local reflections rebaked separately from geometry comparisons. No HMD/performance acceptance.\n");
            Debug.Log("REPRESENTATIVE_BUILD_OK");
        }
        public static void InspectRepresentativeSection()
        {
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();Directory.CreateDirectory(RepOutput);
            string phase=RepArg("-representativePhase","baseline"),hash=AuditHash(WideScene);
            RepHideOverlays();var views=RepCameras();
            var data=new StringBuilder("path,name,active,x,y,z,scale_x,scale_y,scale_z,bounds_min_x,bounds_min_y,bounds_min_z,bounds_max_x,bounds_max_y,bounds_max_z,materials\n");
            foreach(var r in Obj.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var b=r.bounds;
                if(b.max.x< -140||b.min.x> -20||b.max.z<0||b.min.z>100||b.size.x>200||b.size.z>200)continue;
                var t=r.transform;var p=t.position;var s=t.lossyScale;
                data.AppendLine(FacadeCsv(ScenarioValidationPath(t))+","+FacadeCsv(r.name)+","+r.gameObject.activeInHierarchy+","+RepVec(p)+","+RepVec(s)+","+RepVec(b.min)+","+RepVec(b.max)+","+FacadeCsv(string.Join(";",r.sharedMaterials.Where(m=>m!=null).Select(AssetDatabase.GetAssetPath))));
            }
            File.WriteAllText(RepOutput+"/"+phase+"-inventory.csv",data.ToString());
            RepVisibility(views,phase);RepCapture(views,phase);
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();
            if(hash!=AuditHash(WideScene))throw new InvalidOperationException("Inspection changed scene");
            File.WriteAllText(RepOutput+"/"+phase+"-integrity.txt","Scene unchanged="+hash+"\nNo scene save or art mutation. Camera clones only.\n");
            Debug.Log("REPRESENTATIVE_INSPECTION_OK "+phase);
        }
        public static void DiagnoseRepresentativeDarkShape()
        {
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();Directory.CreateDirectory(RepOutput);
            string hash=AuditHash(WideScene);RepHideOverlays();var views=RepCameras();
            var puddle=Obj.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Single(r=>r.name=="B02 runoff shallow puddle");
            File.WriteAllText(RepOutput+"/dark-shape-diagnosis.txt","Candidate="+ScenarioValidationPath(puddle.transform)+"\nBounds="+puddle.bounds+"\nMaterial="+AssetDatabase.GetAssetPath(puddle.sharedMaterial)+"\nCollider count="+puddle.GetComponentsInChildren<Collider>(true).Length+"\nOnly candidate renderer visibility toggled; no light/bake/material/camera change.\n");
            SaveWearCamera(views[4],RepOutput+"/puddle-on-04.png");SaveWearCamera(views[5],RepOutput+"/puddle-on-05.png");
            puddle.enabled=false;
            SaveWearCamera(views[4],RepOutput+"/puddle-off-04.png");SaveWearCamera(views[5],RepOutput+"/puddle-off-05.png");
            if(hash!=AuditHash(WideScene))throw new InvalidOperationException("Puddle diagnostic saved scene");
            Debug.Log("REPRESENTATIVE_DARK_SHAPE_DIAGNOSIS_OK");
        }
        static string RepVec(Vector3 p)=>p.x.ToString("R",Inv)+","+p.y.ToString("R",Inv)+","+p.z.ToString("R",Inv);
        static void RepCapture(List<Camera> views,string phase)
        {
            var rows=new StringBuilder("index,name,x,y,z,forward_x,forward_y,forward_z,fov\n");
            for(int i=0;i<views.Count;i++)
            {
                var c=views[i];SaveWearCamera(c,RepOutput+"/"+phase+"-"+i.ToString("00")+".png");
                rows.AppendLine(i+","+FacadeCsv(c.name)+","+RepVec(c.transform.position)+","+RepVec(c.transform.forward)+","+c.fieldOfView.ToString("R",Inv));
            }
            File.WriteAllText(RepOutput+"/"+phase+"-cameras.csv",rows.ToString());
        }
        static void RepVisibility(List<Camera> views,string phase)
        {
            var meshes=Obj.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(ScenarioValidationRendered).ToArray();
            var cache=new Dictionary<MeshFilter,ScenarioValidationMesh>();var unreadable=new HashSet<MeshFilter>();
            var rows=new StringBuilder("camera,x,y,z,in_frame,clear,first_hit,hit_distance,target_distance\n");
            foreach(var c in views.Where((c,i)=>i==0||i==1||i>=4))
            foreach(float x in new[]{-66f,-54f,-46f,-38f})foreach(float z in new[]{14f,25f,36f,48f})
            {
                var p=new Vector3(x,.15f,z);var v=c.WorldToViewportPoint(p);bool inside=v.z>0&&v.x>=.05f&&v.x<=.95f&&v.y>=.05f&&v.y<=.95f;
                var delta=p-c.transform.position;float length=delta.magnitude,near=float.PositiveInfinity;string hit="none";var ray=new Ray(c.transform.position,delta.normalized);
                foreach(var mf in meshes){float d=ScenarioValidationMeshRay(mf,ray,length,cache,unreadable);if(d<near){near=d;hit=ScenarioValidationPath(mf.transform);}}
                rows.AppendLine(FacadeCsv(c.name)+","+RepVec(p)+","+inside+","+(near>=length-.18f)+","+FacadeCsv(hit)+","+near.ToString("R",Inv)+","+length.ToString("R",Inv));
            }
            File.WriteAllText(RepOutput+"/"+phase+"-visibility.csv",rows.ToString());
            File.WriteAllText(RepOutput+"/"+phase+"-unreadable-meshes.txt",string.Join("\n",unreadable.Select(m=>ScenarioValidationPath(m.transform))));
        }
    }
}
