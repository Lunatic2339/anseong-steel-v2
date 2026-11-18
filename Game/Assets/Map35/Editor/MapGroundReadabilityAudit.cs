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
        [MenuItem("Anseong/Map35/Audit Ground Readability Without Saving")]
        public static void AuditGroundReadability()
        {
            string output=Path.GetFullPath("../docs/validation/map-ground-readability-audit");Directory.CreateDirectory(output);
            string sceneHash=AuditHash(WideScene);
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();WarmSceneParticles();
            var renderers=Obj.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var materials=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null&&m.shader.name=="Anseong/GeologicalGround"&&m.GetFloat("_GroundSurface")>.5f).Distinct().ToArray();
            var before=materials.ToDictionary(m=>AssetDatabase.GetAssetPath(m),m=>AuditHash(AssetDatabase.GetAssetPath(m)));
            var clones=new Dictionary<Material,Material>();Camera near=null;
            try
            {
                viewer.labels.SetActive(false);viewer.routes.SetActive(false);viewer.frusta.SetActive(false);viewer.reservations.SetActive(false);proxies.gameObject.SetActive(false);
                near=Obj.Instantiate(viewer.views[39]);near.name="Audit only ground near pump apron";near.transform.position=new Vector3(110,1.8f,23);
                near.transform.LookAt(new Vector3(101,-.05f,29));near.fieldOfView=65;near.enabled=false;
                var report=new StringBuilder("material,primary_texture,secondary_texture,primary_metres,secondary_metres,mix,macro,normal_strength,wetness\n");
                foreach(var m in materials.OrderBy(m=>m.name))
                    report.AppendLine(m.name+","+m.GetTexture("_GravelTex")?.name+","+m.GetTexture("_RockTex")?.name+","+m.GetFloat("_GravelMetres").ToString(Inv)+","+m.GetFloat("_RockMetres").ToString(Inv)+","+m.GetFloat("_SurfaceMix").ToString(Inv)+","+m.GetFloat("_MacroContrast").ToString(Inv)+","+m.GetFloat("_NormalStrength").ToString(Inv)+","+m.GetFloat("_Wetness").ToString(Inv));
                File.WriteAllText(output+"/current-ground-materials.csv",report.ToString());
                File.WriteAllText(output+"/largest-ground-surfaces.csv","object,material,bounds_x_m,bounds_z_m\n"+
                    string.Join("\n",renderers.Where(r=>r.sharedMaterials.Any(m=>materials.Contains(m))).OrderByDescending(r=>r.bounds.size.x*r.bounds.size.z).Take(20)
                    .Select(r=>r.name+","+r.sharedMaterial.name+","+r.bounds.size.x.ToString(Inv)+","+r.bounds.size.z.ToString(Inv))));
                int[] views={9,12,28,39};
                foreach(var index in views)SaveWearCamera(viewer.views[index],output+"/current-"+index.ToString("00")+".png");
                SaveWearCamera(near,output+"/current-near.png");
                foreach(var m in materials)
                {
                    var clone=Obj.Instantiate(m);clone.name=m.name+" temporary 4x tile diagnostic";
                    clone.SetFloat("_GravelMetres",m.GetFloat("_GravelMetres")*4);clone.SetFloat("_RockMetres",m.GetFloat("_RockMetres")*4);clones.Add(m,clone);
                }
                foreach(var r in renderers)r.sharedMaterials=r.sharedMaterials.Select(m=>m!=null&&clones.TryGetValue(m,out var clone)?clone:m).ToArray();
                foreach(var index in views)SaveWearCamera(viewer.views[index],output+"/tile4x-diagnostic-"+index.ToString("00")+".png");
                SaveWearCamera(near,output+"/tile4x-diagnostic-near.png");
                var manifest=new StringBuilder("Diagnostic only. No production material/scene saved, no rebake/player build.\n");
                manifest.AppendLine("A=current; B=both base material metre tile sizes multiplied by four, all other material/light/camera settings retained. This is a controlled scale test, not a proposed final surface or realistic enlarged aggregate.");
                manifest.AppendLine("Views 09 control Z1, 12 control Z3, 28 low transport inspection, 39 pump facade; close inspection at (110,1.8,23) looking at (101,-.05,29), FOV65. Close camera is not an adopted drone route.");
                manifest.AppendLine("Quality="+QualitySettings.names[QualitySettings.GetQualityLevel()]+"; aniso="+QualitySettings.anisotropicFiltering+"; mip_limit="+QualitySettings.globalTextureMipmapLimit);
                manifest.AppendLine("Scene SHA256 before="+sceneHash+"; after="+AuditHash(WideScene));
                foreach(var item in before){string after=AuditHash(item.Key);manifest.AppendLine(item.Key+": unchanged="+(item.Value==after));if(item.Value!=after)throw new InvalidOperationException("Audit changed ground material asset");}
                File.WriteAllText(output+"/manifest.txt",manifest.ToString());
                if(sceneHash!=AuditHash(WideScene))throw new InvalidOperationException("Ground audit changed saved scene");
                Debug.Log("GROUND_READABILITY_AUDIT_OK captures=10 ground_materials="+materials.Length+" scene_unchanged="+sceneHash);
            }
            finally
            {
                if(near!=null)Obj.DestroyImmediate(near.gameObject);
                EditorSceneManager.OpenScene(WideScene);
                foreach(var clone in clones.Values)Obj.DestroyImmediate(clone);
            }
        }
    }
}
