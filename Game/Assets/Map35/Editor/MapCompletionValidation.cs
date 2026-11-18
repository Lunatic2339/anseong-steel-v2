using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Obj=UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        [MenuItem("Anseong/Map35/Build And Review Whole Map Completion")]
        public static void BuildAndReviewWholeMapCompletion()
        {
            BuildWide();ValidateWholeMapCompletion();CaptureWholeMapQualityAudit();CaptureMapMechanismStates();
            Debug.Log("WHOLE_MAP_FINAL_BUILD_REVIEW_OK");
        }

        [MenuItem("Anseong/Map35/Rebake And Review Whole Map Completion")]
        public static void RebakeAndReviewWholeMapCompletion()
        {
            BakeVisualUpgradeAndBuild();ValidateWholeMapCompletion();CaptureWholeMapQualityAudit();CaptureMapMechanismStates();
            Debug.Log("WHOLE_MAP_FINAL_REBAKE_REVIEW_OK");
        }

        [MenuItem("Anseong/Map35/Correct Coast Facing And Review")]
        public static void CorrectCoastFacingAndReview()
        {
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();
            var changed=new HashSet<Mesh>();int flipped=0;
            foreach(var filter in Obj.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(f=>f.name.StartsWith("Integration coastal rock volume",StringComparison.Ordinal)||f.name=="Integration curved shoreline contact wash"))
            {
                var mesh=filter.sharedMesh;if(mesh==null||changed.Contains(mesh))continue;
                if(!AssetDatabase.GetAssetPath(mesh).StartsWith(Base,StringComparison.Ordinal))throw new InvalidOperationException("Coast correction must not edit a source model");
                var vertices=mesh.vertices;var matrix=filter.transform.localToWorldMatrix;
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var indices=mesh.GetTriangles(sub);
                    for(int k=0;k<indices.Length;k+=3)
                    {
                        var a=matrix.MultiplyPoint3x4(vertices[indices[k]]);var b=matrix.MultiplyPoint3x4(vertices[indices[k+1]]);var c=matrix.MultiplyPoint3x4(vertices[indices[k+2]]);
                        if(Vector3.Cross(b-a,c-a).y<0){int swap=indices[k+1];indices[k+1]=indices[k+2];indices[k+2]=swap;flipped++;}
                    }
                    mesh.SetTriangles(indices,sub);
                }
                mesh.RecalculateNormals();mesh.RecalculateBounds();var normals=mesh.normals;var tangents=new Vector4[normals.Length];
                for(int i=0;i<normals.Length;i++)
                {var t=Vector3.Cross(Vector3.forward,normals[i]);if(t.sqrMagnitude<.01f)t=Vector3.Cross(Vector3.up,normals[i]);t.Normalize();tangents[i]=new Vector4(t.x,t.y,t.z,-1);}
                mesh.tangents=tangents;EditorUtility.SetDirty(mesh);changed.Add(mesh);
            }
            File.WriteAllText(WideOutput+"/coast-facing-check.txt","Derived coast meshes corrected="+changed.Count+"\nDownward triangles flipped="+flipped+"\nIndividual triangles face world-up. Source FBXs unchanged; no Cull Off workaround.\n");
            ValidateFiniteMapGeometry();AssetDatabase.SaveAssets();WarmSceneParticles();BakeLocalReflections();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
            Capture(WideOutput);viewer.Select(0);viewer.labels.SetActive(true);viewer.routes.SetActive(true);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{WideScene},locationPathName="../Builds/MapWide/AnseongMapWide.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Final coast viewer build failed");
            ValidateWholeMapCompletion();CaptureWholeMapQualityAudit();CaptureMapMechanismStates();
            Debug.Log("COAST_FINAL_CORRECTION_REVIEW_OK");
        }

        static void CaptureMapMechanismStates()
        {
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();WarmSceneParticles();
            string output=Path.GetFullPath("../docs/validation/map-whole-upgrade-review");Directory.CreateDirectory(output);
            viewer.labels.SetActive(false);viewer.routes.SetActive(false);viewer.frusta.SetActive(false);viewer.reservations.SetActive(false);proxies.gameObject.SetActive(false);
            foreach(bool late in new[]{false,true})
            {
                viewer.SetLateState(late);
                foreach(int index in new[]{5,12,13})
                {viewer.Select(index);SaveWearCamera(viewer.views[index],Path.Combine(output,"mechanism-"+index.ToString("00")+(late?"-open-settled.png":"-closed-suspended.png")));}
            }
            var dynamicRenderers=new HashSet<Renderer>(viewer.coil.GetComponentsInChildren<Renderer>(true).Concat(viewer.lateCoil.GetComponentsInChildren<Renderer>(true)));
            int included=Obj.FindObjectsByType<LODGroup>(FindObjectsSortMode.None).SelectMany(g=>g.GetLODs()).SelectMany(l=>l.renderers).Count(r=>dynamicRenderers.Contains(r));
            var movingRenderers=dynamicRenderers.Concat(viewer.plate.GetComponentsInChildren<Renderer>(true)).ToArray();
            int bakedMoving=movingRenderers.Count(r=>(GameObjectUtility.GetStaticEditorFlags(r.gameObject)&StaticEditorFlags.ContributeGI)!=0);
            var settledBounds=viewer.lateCoil.GetComponentsInChildren<MeshRenderer>(true).Select(r=>r.bounds.min.y).DefaultIfEmpty(-999).Min();
            File.WriteAllText(Path.Combine(output,"mechanism-state-check.txt"),"Coil renderers included in fixed building LODs: "+included+"\nMoving I1/coil renderers marked ContributeGI: "+bakedMoving+"\nSettled coil lowest renderer bounds y="+settledBounds.ToString("F4",Inv)+"; R1 slab top=0.6\nI1 pivot closed=(0,24,8), open=(-22,24,8); continuous chain pickup follows plate.\nStatic state previews, not animated machinery/network/gameplay acceptance.\n");
            if(included!=0||bakedMoving!=0||settledBounds<.59f)throw new InvalidOperationException("Moving apparatus GI, LOD or settlement regression");
            EditorSceneManager.OpenScene(WideScene);
        }

        [MenuItem("Anseong/Map35/Validate Whole Map Completion")]
        public static void ValidateWholeMapCompletion()
        {
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();
            ValidateWide();ValidateMaintenanceAccess();AuditVisualUpgrade();
            ValidateEnvironmentMeshSightlines();
            var smoke=Obj.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            File.WriteAllText(WideOutput+"/particle-material-check.csv","object,shader,render_queue\n"+
                string.Join("\n",smoke.Select(r=>r.name.Replace(',','_')+","+(r.sharedMaterial==null?"MISSING":r.sharedMaterial.shader.name)+","+(r.sharedMaterial==null?0:r.sharedMaterial.renderQueue))));
            var lights=Obj.FindObjectsByType<Light>(FindObjectsSortMode.None);
            File.WriteAllText(WideOutput+"/installed-lighting.csv","name,type,intensity,range,shadows\n"+
                string.Join("\n",lights.Select(l=>l.name.Replace(',','_')+","+l.type+","+l.intensity+","+l.range+","+l.shadows)));
            Debug.Log("WHOLE_MAP_COMPLETION_VALIDATION_OK cameras="+viewer.views.Length+" lights="+lights.Length);
        }

        static void ValidateFiniteMapGeometry()
        {
            var meshes=Obj.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Select(f=>f.sharedMesh).Where(m=>m!=null).Distinct().ToArray();
            var bad=new List<string>();int vertices=0;
            foreach(var mesh in meshes)
            {
                foreach(var p in mesh.vertices)
                {
                    vertices++;
                    if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z))
                    {bad.Add(mesh.name);break;}
                }
            }
            File.WriteAllText(WideOutput+"/finite-geometry-check.txt","Distinct meshes="+meshes.Length+"\nVertices inspected="+vertices+"\nNon-finite meshes="+bad.Count+"\n"+string.Join("\n",bad));
            if(bad.Count>0)throw new InvalidOperationException("Invalid geometry before light bake: "+string.Join("; ",bad));
        }

        static void ValidateEnvironmentMeshSightlines()
        {
            var skip=new HashSet<Renderer>();
            foreach(var group in Obj.FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
                foreach(var lod in group.GetLODs().Skip(1))foreach(var r in lod.renderers)skip.Add(r);
            var candidates=Obj.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(r=>!skip.Contains(r)&&r.enabled&&r.GetComponent<TextMesh>()==null&&
                    !r.transform.IsChildOf(proxies)&&!r.transform.IsChildOf(labels)&&!r.transform.IsChildOf(routes)&&!r.transform.IsChildOf(volumes))
                .Select(r=>(renderer:r,filter:r.GetComponent<MeshFilter>())).Where(p=>p.filter!=null&&p.filter.sharedMesh!=null).ToArray();
            var cached=new Dictionary<Mesh,(Vector3[] vertices,int[][] triangles)>();
            var queries=new Dictionary<int,string[]>{{2,new[]{"I1 push"}},{3,new[]{"T1 shell"}},{4,new[]{"I6 neck","Coil edge"}},{8,new[]{"I1 push"}},{9,new[]{"I1 status"}},{10,new[]{"T1 shell","T2 nerve"}},
                {11,new[]{"T2 nerve"}},{12,new[]{"I6 neck","Coil edge"}},{13,new[]{"P02 latch","Coil edge"}},
                {5,new[]{"I1 latch"}},{6,new[]{"T2 nerve"}},{7,new[]{"P02 latch"}}};
            var report=new StringBuilder("camera,target,sample,environment_occluded,nearest_surface,distance_from_target\n");
            foreach(var query in queries)
            foreach(string name in query.Value)
            {
                var camera=viewer.views[query.Key];var point=targets[name];
                // Report raw intersections rather than exempting all geometry near a target.
                var offsets=new[]{Vector3.zero,camera.transform.right*.7f,-camera.transform.right*.7f,camera.transform.up*.7f,-camera.transform.up*.7f};
                for(int sample=0;sample<offsets.Length;sample++)
                {
                    Vector3 end=point+offsets[sample];float length=Vector3.Distance(end,camera.transform.position);
                    var ray=new Ray(camera.transform.position,(end-camera.transform.position).normalized);
                    float nearest=length;string hit="";
                    foreach(var pair in candidates)
                    {
                        if((camera.cullingMask&(1<<pair.renderer.gameObject.layer))==0||!pair.renderer.bounds.IntersectRay(ray,out float broad)||broad>nearest)continue;
                        var mesh=pair.filter.sharedMesh;
                        if(!cached.TryGetValue(mesh,out var data))
                        {data=(mesh.vertices,Enumerable.Range(0,mesh.subMeshCount).Select(s=>mesh.GetTriangles(s)).ToArray());cached[mesh]=data;}
                        var matrix=pair.filter.transform.localToWorldMatrix;
                        var materials=pair.renderer.sharedMaterials;
                        for(int sub=0;sub<data.triangles.Length;sub++)
                        {
                            if(sub>=materials.Length||materials[sub]==null||materials[sub].renderQueue>2500)continue;
                            var indices=data.triangles[sub];
                            for(int k=0;k<indices.Length;k+=3)
                            {
                                float distance=RayTriangle(ray,matrix.MultiplyPoint3x4(data.vertices[indices[k]]),matrix.MultiplyPoint3x4(data.vertices[indices[k+1]]),matrix.MultiplyPoint3x4(data.vertices[indices[k+2]]));
                                if(distance>.3f&&distance<nearest){nearest=distance;hit=pair.renderer.name;}
                            }
                        }
                    }
                    report.AppendLine(query.Key+","+name+","+sample+","+(length-nearest>.18f)+","+hit.Replace(',','_')+","+(length-nearest).ToString("F3",Inv));
                }
            }
            File.WriteAllText(WideOutput+"/environment-mesh-sightlines.csv",report.ToString());
            File.WriteAllText(WideOutput+"/environment-mesh-sightlines-scope.txt",
                "Centre and four 0.7m camera-plane offsets at the existing role targets. Tests actual LOD0 opaque/environment mesh triangles, including decorative geometry without colliders.\n"+
                "Phase proxies, labels, transparent submeshes, particle renderers and far LODs excluded. No blanket exemption around target; raw intersections closer than target minus 0.18m are recorded, including intended target apparatus.\n"+
                "A diagnostic occlusion table; not full animated boss, arm envelope, transparency, HMD or gameplay acceptance. K3 coil can self-occlude; K3b is the companion view.\n");
        }

        static float RayTriangle(Ray ray,Vector3 a,Vector3 b,Vector3 c)
        {
            var e1=b-a;var e2=c-a;var p=Vector3.Cross(ray.direction,e2);float determinant=Vector3.Dot(e1,p);
            if(Mathf.Abs(determinant)<.000001f)return -1;
            float inverse=1/determinant;var s=ray.origin-a;float u=Vector3.Dot(s,p)*inverse;if(u<0||u>1)return -1;
            var q=Vector3.Cross(s,e1);float v=Vector3.Dot(ray.direction,q)*inverse;if(v<0||u+v>1)return -1;
            return Vector3.Dot(e2,q)*inverse;
        }
    }
}
