using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        // Inspection only: never regenerate, bake, save the scene, or overwrite production captures.
        [MenuItem("Anseong/Map35/Capture Whole Map Quality Audit")]
        public static void CaptureWholeMapQualityAudit()
        {
            string output=Path.GetFullPath("../docs/validation/map-whole-upgrade-review");
            Directory.CreateDirectory(output);
            string before=AuditHash(WideScene);
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();WarmSceneParticles();
            var hidden=new List<(GameObject gameObject,bool active)>();
            Action<GameObject> hide=go=>
            {
                if(go==null||hidden.Any(h=>h.gameObject==go))return;
                hidden.Add((go,go.activeSelf));go.SetActive(false);
            };
            try
            {
                hide(viewer.labels);hide(viewer.routes);hide(viewer.frusta);hide(viewer.reservations);
                hide(proxies.gameObject);hide(viewer.plate.gameObject);
                var detailRoot=GameObject.Find("02 Interaction markers and scale details").transform;
                foreach(var name in new[]{"plate rail","I1 push face","I1 control status","I1 drone latch","T1 front shell","T2 west nerve","T3 low neck","E01 control emitter","P02 crane latch","Coil east edge","I6 final weakpoint","1.8m person scale"})
                    foreach(Transform item in detailRoot)if(item.name==name)hide(item.gameObject);
                var report=new StringBuilder("Inspection captures only; original production screenshots remain in map-wide.\n");
                report.AppendLine("Only proxies, temporary target spheres, moving sliding plate and debug overlays are hidden. Fixed I1 rail/equipment, environmental geometry, cranes, coils, materials, lighting, smoke and signage are retained.\nThis capture method does not save the scene, edit materials, rebake or build the player; the integration wrapper performs those operations beforehand.");
                report.AppendLine("Temporarily hidden objects:");
                foreach(var item in hidden)report.AppendLine(item.gameObject.name+"; previously active="+item.active);
                for(int i=0;i<viewer.views.Length;i++)
                {
                    viewer.Select(i);
                    SaveWearCamera(viewer.views[i],Path.Combine(output,"environment-"+i.ToString("00")+".png"));
                }
                // Temporary facade/shore/damaged-harbor inspection, without altering saved role cameras.
                var inspectionViews=new[]{
                    ("b03-southeast",new Vector3(118,10,18),new Vector3(90,6,45)),
                    ("b03-northwest",new Vector3(62,10,70),new Vector3(90,6,45)),
                    ("b03-overhead",new Vector3(90,48,95),new Vector3(90,5,45)),
                    ("b03-vr-head-turn",new Vector3(0,29.75f,0),new Vector3(90,6,45)),
                    ("harbor-damaged-deck",new Vector3(395,10,399),new Vector3(437,0,426)),
                    ("coast-low-seaward",new Vector3(486,1.5f,810),new Vector3(448,0,812)),
                    ("coast-oblique-south",new Vector3(450,3,776),new Vector3(448,0,812))
                };
                foreach(var item in inspectionViews)
                {
                    var camera=UnityEngine.Object.Instantiate(viewer.views[18]);
                    camera.name="Audit only "+item.Item1;camera.transform.position=item.Item2;
                    camera.transform.LookAt(item.Item3);camera.fieldOfView=65;camera.enabled=false;
                    try{SaveWearCamera(camera,Path.Combine(output,item.Item1+".png"));}
                    finally{UnityEngine.Object.DestroyImmediate(camera.gameObject);}
                    report.AppendLine(item.Item1+": camera="+item.Item2+", look="+item.Item3+", vertical FOV=65; inspection only, not adopted gameplay camera.");
                }
                report.AppendLine("Scene SHA256 before="+before);
                report.AppendLine("Scene SHA256 after="+AuditHash(WideScene));
                report.AppendLine("Capture count="+viewer.views.Length+" matching production views plus "+inspectionViews.Length+" facade/shore/harbor inspection views; size=1600x1000; Unity Editor camera renders, not HMD/player/performance validation.");
                File.WriteAllText(Path.Combine(output,"capture-manifest.txt"),report.ToString());
                if(before!=AuditHash(WideScene))throw new InvalidOperationException("Audit unexpectedly changed scene file.");
                Debug.Log("MAP_WHOLE_QUALITY_AUDIT_OK captures="+viewer.views.Length+" scene_unchanged="+before);
            }
            finally
            {
                foreach(var item in hidden)item.gameObject.SetActive(item.active);
                // Discard all in-memory camera/particle/visibility changes without saving.
                EditorSceneManager.OpenScene(WideScene);
            }
        }

        static string AuditHash(string path)
        {
            using(var stream=File.OpenRead(path))
            using(var hash=SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
    }
}
