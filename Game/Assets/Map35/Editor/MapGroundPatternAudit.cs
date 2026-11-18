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
        [Serializable] sealed class GroundPatternAssetHash
        {
            public string path,sha256_before,sha256_after;
            public bool unchanged;
        }
        [Serializable] sealed class GroundPatternCapture
        {
            public string condition,file,camera_name;
            public int camera_index;
            public Vector3 position,forward;
            public float field_of_view;
        }
        [Serializable] sealed class GroundPatternManifest
        {
            public string generated_utc,scope,status,scene,scene_sha256_before,scene_sha256_after;
            public bool scene_unchanged,all_original_materials_unchanged,all_original_masks_unchanged;
            public int capture_count,purpose_material_count,assigned_renderer_count;
            public int[] camera_indices;
            public string[] conditions;
            public GroundPatternCapture[] captures;
            public GroundPatternAssetHash[] original_material_assets,original_mask_assets;
        }

        // Controlled visual diagnosis only. No source material, texture or saved scene is modified.
        [MenuItem("Anseong/Map35/Audit Ground Pattern Causes Without Saving")]
        public static void AuditGroundPatternCauses()
        {
            string output=Path.GetFullPath("../docs/validation/map-ground-realism-audit");
            Directory.CreateDirectory(output);
            string before=AuditHash(WideScene);
            // Include every standalone material asset in Assets, not only this diagnostic's targets.
            var materialHashes=AssetDatabase.FindAssets("t:Material",new[]{"Assets"})
                .Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".mat",StringComparison.OrdinalIgnoreCase))
                .Distinct().OrderBy(p=>p).Select(p=>new GroundPatternAssetHash{path=p,sha256_before=AuditHash(p)}).ToArray();
            var materialClones=new Dictionary<Material,Material>();
            var maskClones=new Dictionary<Texture2D,Texture2D>();
            var captures=new List<GroundPatternCapture>();
            GroundPatternAssetHash[] maskHashes=new GroundPatternAssetHash[0];
            int[] indices={3,9,12,28,37};
            string[] conditions={"base","no-batch","no-repair","no-batch-no-repair"};
            int assigned=0;
            try
            {
                EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();WarmSceneParticles();
                if(viewer.scenario!=null)viewer.scenario.gameObject.SetActive(false);
                proxies.gameObject.SetActive(false);
                viewer.labels.SetActive(false);viewer.routes.SetActive(false);viewer.frusta.SetActive(false);viewer.reservations.SetActive(false);
                var renderers=Obj.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .Where(r=>r.gameObject.scene.IsValid()).ToArray();
                var originals=renderers.SelectMany(r=>r.sharedMaterials)
                    .Where(m=>m!=null&&m.shader!=null&&m.shader.name=="Anseong/PurposeGround").Distinct().ToArray();
                if(originals.Length==0)throw new InvalidOperationException("No PurposeGround materials found for pattern diagnostic");
                var masks=originals.Select(m=>m.GetTexture("_PurposeMask") as Texture2D).Where(t=>t!=null).Distinct().ToArray();
                if(masks.Length==0)throw new InvalidOperationException("No readable PurposeGround masks found");
                maskHashes=masks.Select(AssetDatabase.GetAssetPath).Where(p=>!string.IsNullOrEmpty(p)).Distinct().OrderBy(p=>p)
                    .Select(p=>new GroundPatternAssetHash{path=p,sha256_before=AuditHash(p)}).ToArray();
                var materialReport=new StringBuilder("material,slab_variation,slab_metres_x,slab_metres_z,purpose_mask,mask_width,mask_height\n");
                foreach(var source in masks)
                {
                    if(!source.isReadable)throw new InvalidOperationException("Purpose mask is not readable: "+source.name);
                    var clone=Obj.Instantiate(source);clone.name=source.name+" audit only red removed";clone.hideFlags=HideFlags.DontSave;
                    var pixels=clone.GetPixels32();for(int i=0;i<pixels.Length;i++)pixels[i].r=0;
                    clone.SetPixels32(pixels);clone.Apply(true,false);maskClones.Add(source,clone);
                }
                foreach(var source in originals)
                {
                    var clone=Obj.Instantiate(source);clone.name=source.name+" audit only";clone.hideFlags=HideFlags.DontSave;
                    materialClones.Add(source,clone);var mask=source.GetTexture("_PurposeMask") as Texture2D;var bay=source.GetVector("_SlabMetres");
                    materialReport.AppendLine(source.name.Replace(',','_')+","+source.GetFloat("_SlabVariation").ToString(Inv)+","+bay.x.ToString(Inv)+","+bay.y.ToString(Inv)+","+(mask==null?"none":mask.name)+","+(mask==null?0:mask.width)+","+(mask==null?0:mask.height));
                }
                foreach(var renderer in renderers)
                {
                    var slots=renderer.sharedMaterials;bool changed=false;
                    for(int slot=0;slot<slots.Length;slot++)
                        if(slots[slot]!=null&&materialClones.TryGetValue(slots[slot],out var clone)){slots[slot]=clone;changed=true;}
                    if(changed){renderer.sharedMaterials=slots;assigned++;}
                }
                File.WriteAllText(output+"/source-pattern-materials.csv",materialReport.ToString());
                foreach(string condition in conditions)
                {
                    bool noBatch=condition=="no-batch"||condition=="no-batch-no-repair";
                    bool noRepair=condition=="no-repair"||condition=="no-batch-no-repair";
                    foreach(var pair in materialClones)
                    {
                        var mask=pair.Key.GetTexture("_PurposeMask") as Texture2D;
                        pair.Value.SetFloat("_SlabVariation",noBatch?0:pair.Key.GetFloat("_SlabVariation"));
                        pair.Value.SetTexture("_PurposeMask",noRepair&&mask!=null?maskClones[mask]:mask);
                    }
                    foreach(int index in indices)
                    {
                        viewer.Select(index);var camera=viewer.views[index];string file=condition+"-"+index.ToString("00")+".png";
                        SaveWearCamera(camera,Path.Combine(output,file));
                        captures.Add(new GroundPatternCapture{condition=condition,file=file,camera_name=camera.name,camera_index=index,position=camera.transform.position,forward=camera.transform.forward,field_of_view=camera.fieldOfView});
                    }
                }
            }
            finally
            {
                // Discard all temporary scene assignments before releasing the transient materials.
                EditorSceneManager.OpenScene(WideScene);
                foreach(var clone in materialClones.Values)if(clone!=null)Obj.DestroyImmediate(clone);
                foreach(var clone in maskClones.Values)if(clone!=null)Obj.DestroyImmediate(clone);
            }
            foreach(var item in materialHashes.Concat(maskHashes))
            {item.sha256_after=AuditHash(item.path);item.unchanged=item.sha256_before==item.sha256_after;}
            string after=AuditHash(WideScene);
            bool clean=before==after&&materialHashes.All(h=>h.unchanged)&&maskHashes.All(h=>h.unchanged);
            var manifest=new GroundPatternManifest{
                generated_utc=DateTime.UtcNow.ToString("o"),status=clean&&captures.Count==20?"diagnostic_complete_assets_unchanged":"validation_failed",
                scope="Four temporary material conditions at five unchanged cameras. 1600x1000 mono Editor camera renders with current lighting/post-processing. Scenario actors/installations and legacy scale proxies/debug overlays hidden. No new ground/map import, final design selection, scene/material save, rebake, player build, HMD or AAA comparison.",
                scene=WideScene,scene_sha256_before=before,scene_sha256_after=after,scene_unchanged=before==after,
                all_original_materials_unchanged=materialHashes.All(h=>h.unchanged),all_original_masks_unchanged=maskHashes.All(h=>h.unchanged),
                capture_count=captures.Count,purpose_material_count=materialClones.Count,assigned_renderer_count=assigned,
                camera_indices=indices,conditions=conditions,captures=captures.ToArray(),original_material_assets=materialHashes,original_mask_assets=maskHashes};
            File.WriteAllText(output+"/manifest.json",JsonUtility.ToJson(manifest,true));
            var text=new StringBuilder(manifest.scope+"\n");
            text.AppendLine("base: original cloned material settings; no-batch: _SlabVariation=0 only; no-repair: cloned mask red=0 only, G/B/A preserved; no-batch-no-repair: both changes.");
            text.AppendLine("Camera03 VR Z2,09 K1 overview,12 K3 overview,28 low road,37 B02 near. These are fixed inspection views, not new player routes.");
            text.AppendLine("Capture count="+captures.Count+"; PurposeGround materials="+materialClones.Count+"; assigned renderers="+assigned);
            text.AppendLine("Scene unchanged="+manifest.scene_unchanged+" SHA256="+after);
            text.AppendLine("Original standalone .mat files unchanged="+manifest.all_original_materials_unchanged+" count="+materialHashes.Length);
            text.AppendLine("Original purpose-mask assets unchanged="+manifest.all_original_masks_unchanged+" count="+maskHashes.Length);
            text.AppendLine("These experiments remove information to locate a visual cause; none is automatically a recommended final ground design.");
            File.WriteAllText(output+"/manifest.txt",text.ToString());
            if(!clean||captures.Count!=20)throw new InvalidOperationException("Ground pattern diagnostic integrity failed; inspect manifest.json");
            Debug.Log("GROUND_PATTERN_AUDIT_OK captures=20 scene_unchanged="+after+" original_materials_unchanged="+materialHashes.Length);
        }
    }
}
