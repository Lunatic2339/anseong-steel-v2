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
        [MenuItem("Anseong/Map35/Preview Local Surface Wear")]
        public static void PreviewLocalSurfaceWear()
        {
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();WarmSceneParticles();
            CaptureLocalWearComparison();Debug.Log("B02_SURFACE_WEAR_PREVIEW_OK");
        }
        static void CaptureLocalWearComparison()
        {
            var renderers=Existing("B02").GetComponentsInChildren<MeshRenderer>();
            var materials=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null&&m.HasProperty("_WearAmount")&&m.GetFloat("_WearAmount")>0).Distinct().ToArray();
            if(materials.Length!=3)throw new InvalidOperationException("Expected three B02 partial-wear paint variants.");
            int valid=0;var metadata=new List<Vector4>();
            foreach(var renderer in renderers)
            {
                if(!renderer.sharedMaterials.Any(m=>materials.Contains(m)))continue;
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;metadata.Clear();mesh.GetUVs(2,metadata);
                if(metadata.Count!=mesh.vertexCount||!metadata.Any(v=>v.z>.1f&&v.w>.1f))
                    throw new InvalidOperationException("Missing panel-scale UV3 data on "+renderer.name);
                valid++;
            }
            var amounts=materials.Select(m=>m.GetFloat("_WearAmount")).ToArray();
            var camera=viewer.views.First(c=>c.name.Contains("painted and corroded cladding"));
            viewer.Select(Array.IndexOf(viewer.views,camera));labels.gameObject.SetActive(false);routes.gameObject.SetActive(false);
            viewer.frusta.SetActive(false);viewer.reservations.SetActive(false);
            try
            {
                foreach(var material in materials)material.SetFloat("_WearAmount",0);
                SaveWearCamera(camera,WideOutput+"/surface-wear-before.png");
            }
            finally
            {
                for(int i=0;i<materials.Length;i++)materials[i].SetFloat("_WearAmount",amounts[i]);
            }
            SaveWearCamera(camera,WideOutput+"/surface-wear-after.png");
            var report=new StringBuilder("B02 local paint/rust blend. Shared camera/geometry/baked lighting; layer disabled/enabled only.\n");
            report.AppendLine("Active B02 paint renderers with panel UV3: "+valid);
            foreach(var material in materials)
            {
                var tex=material.GetTexture("_RustAlbedo");
                report.AppendLine(material.name+": wear="+material.GetFloat("_WearAmount")+", rust texture="+tex.width+"x"+tex.height+", rust tile metres="+material.GetFloat("_RustTileMetres"));
                if(material.GetTexture("_RustNormal")==null||material.GetTexture("_RustRoughness")==null)throw new InvalidOperationException("Missing rust PBR channels");
            }
            report.AppendLine("UV0 remains physical metres; UV2 remains lightmap UV; UV3 stores panel fraction and world-metre face size. Source pixels were not upscaled.");
            report.AppendLine("Base paint retained, rust metalness zero; local albedo/normal/roughness mixed. Shader cost/HMD not validated.");
            File.WriteAllText(WideOutput+"/surface-wear-audit.txt",report.ToString());
            var taskLight=Obj.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.name=="B02 warm emergency task light");
            if(taskLight!=null)
            {
                var bayCamera=viewer.views.First(c=>c.name.Contains("connected maintenance bay"));
                viewer.Select(Array.IndexOf(viewer.views,bayCamera));
                bool enabled=taskLight.enabled;
                try{taskLight.enabled=false;SaveWearCamera(bayCamera,WideOutput+"/work-light-off.png");}
                finally{taskLight.enabled=enabled;}
                SaveWearCamera(bayCamera,WideOutput+"/work-light-on.png");
                File.WriteAllText(WideOutput+"/work-light-audit.txt","One warm generator-supplied spotlight, intensity "+taskLight.intensity+", range "+taskLight.range+", angle "+taskLight.spotAngle+".\nSame camera and baked indirect/reflections in both images. Only direct task light toggled. No HMD cost validation.\n");
            }
        }

        static void SaveWearCamera(Camera camera,string path)
        {
            var previous=RenderTexture.active;var previousTarget=camera.targetTexture;
            var target=new RenderTexture(1600,1000,24);var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.aspect=1.6f;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally{camera.targetTexture=previousTarget;RenderTexture.active=previous;target.Release();Obj.DestroyImmediate(target);Obj.DestroyImmediate(pixels);}
        }
    }
}
