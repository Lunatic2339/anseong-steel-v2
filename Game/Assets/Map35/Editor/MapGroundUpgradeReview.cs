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
        static readonly int[] GroundReviewViews={2,4,5,6,9,10,12,14,17,28,29,35,37,39,40,42};
        static string GroundReviewOutput=>Path.GetFullPath("../docs/validation/map-ground-upgrade");

        [MenuItem("Anseong/Map35/Apply Ground Art And Preview")]
        public static void ApplyGroundArtAndPreview()
        {
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();
            Directory.CreateDirectory(GroundReviewOutput);
            if(!File.Exists(GroundReviewOutput+"/before-09.png"))
            {
                CaptureGroundArtViews("before");
                File.WriteAllText(GroundReviewOutput+"/before-scene-sha256.txt",AuditHash(WideScene));
            }
            string colliderBefore=GroundColliderSignature();
            ApplyGroundSurfaceUpgrade();ApplyGroundEdgeUpgrade();RefineGroundPassTaskLighting();
            string colliderAfter=GroundColliderSignature();
            File.WriteAllText(GroundReviewOutput+"/collider-preservation.txt","Existing collider bounds unchanged="+(colliderBefore==colliderAfter)+"\nSurface material changes and non-collision boundary details only. This is not an animated actor or HMD test.\n");
            if(colliderBefore!=colliderAfter)throw new InvalidOperationException("Ground art unexpectedly changed collision geometry");
            ValidateFiniteMapGeometry();AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
            CaptureGroundArtViews("preview");
            ValidateGroundArt();
            EditorSceneManager.OpenScene(WideScene);
            Debug.Log("GROUND_ART_PREVIEW_OK");
        }

        [MenuItem("Anseong/Map35/Bake Build And Review Ground Art")]
        public static void BakeBuildAndReviewGroundArt()
        {
            BakeVisualUpgradeAndBuild();ValidateWholeMapCompletion();
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();
            CaptureGroundArtViews("after");ValidateGroundArt();
            CaptureWholeMapQualityAudit();CaptureMapMechanismStates();
            File.WriteAllText(GroundReviewOutput+"/final-scene-sha256.txt",AuditHash(WideScene));
            Debug.Log("GROUND_ART_FINAL_BUILD_REVIEW_OK");
        }

        static void CaptureGroundArtViews(string phase)
        {
            WarmSceneParticles();
            var states=new List<(GameObject obj,bool active)>();
            foreach(var obj in new[]{viewer.labels,viewer.routes,viewer.frusta,viewer.reservations,proxies.gameObject})
            {states.Add((obj,obj.activeSelf));obj.SetActive(false);}
            Camera near=null;
            try
            {
                foreach(int index in GroundReviewViews)
                {viewer.Select(index);SaveWearCamera(viewer.views[index],GroundReviewOutput+"/"+phase+"-"+index.ToString("00")+".png");}
                near=Obj.Instantiate(viewer.views[39]);near.enabled=false;near.transform.position=new Vector3(110,1.8f,23);
                near.transform.LookAt(new Vector3(101,-.05f,29));near.fieldOfView=65;
                SaveWearCamera(near,GroundReviewOutput+"/"+phase+"-near.png");
                if(phase!="before")
                {
                    near.transform.position=new Vector3(-59,1.8f,21);near.transform.LookAt(new Vector3(-62,-.03f,24));
                    SaveWearCamera(near,GroundReviewOutput+"/"+phase+"-edge-B02.png");
                    near.transform.position=new Vector3(19,1.3f,134);near.transform.LookAt(new Vector3(16,.06f,139));
                    SaveWearCamera(near,GroundReviewOutput+"/"+phase+"-edge-road.png");
                }
            }
            finally
            {
                if(near!=null)Obj.DestroyImmediate(near.gameObject);
                foreach(var item in states)item.obj.SetActive(item.active);
            }
        }

        // The exposed end of the pin faces the drone to the east. The old
        // overhead task light mainly lit the protective shoulder, not this face.
        static void RefineGroundPassTaskLighting()
        {
            var fixedRoot=GameObject.Find("I1 fixed rail and role equipment");if(fixedRoot==null)return;
            const string name="I1 pin face inspection fixture";
            var previous=fixedRoot.transform.Find(name);if(previous!=null)Obj.DestroyImmediate(previous.gameObject);
            var root=Group(name,fixedRoot.transform);
            var cradle=fixedRoot.GetComponentsInChildren<MeshRenderer>().First(r=>r.name=="I1 PC3 latch cradle");
            var steel=cradle.sharedMaterial;
            var white=Material("GroundPass_inspection_lens",new Color(.85f,.9f,.85f));
            white.EnableKeyword("_EMISSION");white.SetColor("_EmissionColor",new Color(.7f,.8f,.72f)*1.4f);
            var origin=new Vector3(11.3f,28.3f,8.8f);var target=new Vector3(9.2f,26,8);
            Beam("Pin light fixed bracket",new Vector3(10.2f,26.8f,4.46f),origin,.08f,steel,root);
            var rotation=Quaternion.LookRotation(target-origin);
            var housing=Box("Shielded pin inspection housing",origin,new Vector3(.42f,.25f,.28f),steel,root,false);housing.transform.rotation=rotation;
            var lens=Box("Recessed pin inspection lens",origin+rotation*new Vector3(0,0,.145f),new Vector3(.31f,.16f,.02f),white,root,false);lens.transform.rotation=rotation;
            var source=new GameObject("Pin face local light").AddComponent<Light>();source.transform.SetParent(root);
            source.transform.SetPositionAndRotation(origin+rotation*new Vector3(0,0,.18f),rotation);
            source.type=LightType.Spot;source.color=new Color(.86f,.94f,1);source.intensity=4.2f;source.range=7;
            source.spotAngle=52;source.innerSpotAngle=30;source.shadows=LightShadows.None;source.renderMode=LightRenderMode.ForcePixel;
            source.lightmapBakeType=LightmapBakeType.Realtime;
        }

        static void ValidateGroundArt()
        {
            var renderers=Obj.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var ground=renderers.Where(r=>r.sharedMaterials.Any(m=>m!=null&&m.shader!=null&&m.shader.name=="Anseong/PurposeGround")).ToArray();
            if(ground.Length==0)throw new InvalidOperationException("Purpose-authored ground materials not assigned");
            var materials=ground.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null&&m.shader.name=="Anseong/PurposeGround").Distinct().ToArray();
            var errors=new List<string>();
            foreach(var material in materials)
                if(ShaderUtil.ShaderHasError(material.shader))errors.Add(material.name+" shader compile error");
            var report=new StringBuilder("Applied purpose-ground renderers="+ground.Length+"\nDerived ground materials="+materials.Length+"\n");
            report.AppendLine("Cameras: "+string.Join(",",GroundReviewViews)+" plus temporary near-ground inspection. 1600x1000, equal transforms/FOV and production post processing.\nBefore/preview retain prior indirect bake; final after includes refreshed Z1 indirect light and local reflections. Proxies/overlays hidden in each; actual I1 plate retained.\nNo new locomotion, collision, boss or networking implementation. Desktop art validation is not HMD performance acceptance.");
            report.AppendLine("Ground shader errors="+errors.Count);
            foreach(var item in errors)report.AppendLine(item);
            File.WriteAllText(GroundReviewOutput+"/ground-art-validation.txt",report.ToString());
            File.WriteAllText(GroundReviewOutput+"/assigned-surfaces.csv","object,material,bounds_min_y,bounds_max_y\n"+
                string.Join("\n",ground.Select(r=>r.name.Replace(',','_')+","+r.sharedMaterial.name+","+r.bounds.min.y.ToString(Inv)+","+r.bounds.max.y.ToString(Inv))));
            if(errors.Count>0)throw new InvalidOperationException(string.Join(";",errors));
        }

        static string GroundColliderSignature()
        {
            Physics.SyncTransforms();
            return string.Join("\n",Obj.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Select(c=>c.GetType().Name+"/"+c.name+"/"+c.enabled+"/"+c.isTrigger+"/"+
                    c.bounds.center.ToString("F5")+"/"+c.bounds.size.ToString("F5"))
                .OrderBy(s=>s,StringComparer.Ordinal));
        }
    }
}
