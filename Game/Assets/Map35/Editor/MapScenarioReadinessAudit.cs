using System;
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
        // Inspection only. All camera/state changes are discarded; no scene or asset save.
        [MenuItem("Anseong/Map35/Audit Scenario Readiness")]
        public static void AuditScenarioReadiness()
        {
            string output=Path.GetFullPath("../docs/validation/map-scenario-readiness");
            Directory.CreateDirectory(output);string before=AuditHash(WideScene);
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();WarmSceneParticles();
            var manifest=new StringBuilder("image,source_camera,x,y,z,look_x,look_y,look_z,fov,state\n");
            var measurements=new StringBuilder("camera,target,distance_m,viewport_x,viewport_y,in_frustum,nominal_2m_marker_height_px,first_collider_before_target\n");
            int count=0;
            try
            {
                viewer.labels.SetActive(false);viewer.routes.SetActive(false);viewer.frusta.SetActive(false);viewer.reservations.SetActive(false);
                viewer.SetLateState(false);Physics.SyncTransforms();
                foreach(int index in new[]{2,3,4,5,6,7,9,10,11,12,13,14,15})
                {
                    viewer.Select(index);var c=viewer.views[index];
                    string name="role-"+index.ToString("00");SaveWearCamera(c,output+"/"+name+".png");count++;
                    AppendScenarioCamera(manifest,name,c,c.transform.position+c.transform.forward,"closed-suspended");
                }
                var inspections=new[]{
                    ("M2-first-fight",2,new Vector3(0,29.75f,65),new Vector3(0,22,95)),
                    ("M6-connector-entry",14,new Vector3(0,29.75f,360),new Vector3(0,22,405)),
                    ("M7-east-turn",14,new Vector3(0,29.75f,405),new Vector3(90,20,405)),
                    ("M8-north-turn",15,new Vector3(90,29.75f,405),new Vector3(90,25,490)),
                    ("M11a-safe-position",4,new Vector3(72,29.75f,504),new Vector3(90,24,555)),
                    ("D9-crane-approach",7,new Vector3(165,54,510),new Vector3(140,48,555))
                };
                foreach(var item in inspections)
                {
                    var c=Obj.Instantiate(viewer.views[item.Item2]);c.enabled=false;
                    c.transform.position=item.Item3;c.transform.LookAt(item.Item4);c.fieldOfView=70;
                    try
                    {
                        SaveWearCamera(c,output+"/"+item.Item1+".png");count++;
                        AppendScenarioCamera(manifest,item.Item1,c,item.Item4,"closed-suspended");
                        if(item.Item1=="M11a-safe-position")
                        {
                            viewer.SetLateState(true);SaveWearCamera(c,output+"/M11a-after-settlement.png");count++;
                            AppendScenarioCamera(manifest,"M11a-after-settlement",c,item.Item4,"open-settled-static-toggle");
                            viewer.SetLateState(false);
                        }
                    }
                    finally{Obj.DestroyImmediate(c.gameObject);}
                }
                viewer.SetLateState(true);
                foreach(int index in new[]{4,12,13})
                {
                    viewer.Select(index);var c=viewer.views[index];string name="settled-role-"+index.ToString("00");
                    SaveWearCamera(c,output+"/"+name+".png");count++;
                    AppendScenarioCamera(manifest,name,c,c.transform.position+c.transform.forward,"open-settled-static-toggle");
                }
                viewer.SetLateState(false);Physics.SyncTransforms();
                var targetPoints=new[]{
                    ("T1 shell",new Vector3(-12,32,270),new[]{3,10,14}),
                    ("T2 west nerve",new Vector3(-20,34,283),new[]{3,6,10,11}),
                    ("T3 low neck",new Vector3(0,24,263),new[]{3,6,10,11}),
                    ("P02 crane latch",new Vector3(140,48,555),new[]{4,7,12,13}),
                    ("I6 final neck",new Vector3(90,24,512),new[]{4,7,12,13}),
                    ("M11a ground",new Vector3(72,.9f,504),new[]{4,12,13})
                };
                foreach(var target in targetPoints)foreach(int index in target.Item3)
                {
                    var c=viewer.views[index];c.aspect=1.6f;var p=target.Item2;var v=c.WorldToViewportPoint(p);
                    var hi=c.WorldToScreenPoint(p+c.transform.up);var lo=c.WorldToScreenPoint(p-c.transform.up);
                    float pixels=v.z>0?1000f/(2*Mathf.Tan(c.fieldOfView*Mathf.Deg2Rad*.5f))*2/v.z:0;
                    var ray=p-c.transform.position;
                    var hit=Physics.RaycastAll(c.transform.position,ray.normalized,Mathf.Max(0,ray.magnitude-.15f),c.cullingMask,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance).FirstOrDefault();
                    string blocker=hit.collider==null?"":ScenarioObjectPath(hit.collider.transform);
                    measurements.AppendLine(index+","+target.Item1+","+ray.magnitude.ToString("F3",Inv)+","+v.x.ToString("F3",Inv)+","+v.y.ToString("F3",Inv)+","+(v.z>0&&v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1)+","+pixels.ToString("F2",Inv)+","+blocker.Replace(',','_'));
                }
                File.WriteAllText(output+"/captures.csv",manifest.ToString());
                File.WriteAllText(output+"/target-projections.csv",measurements.ToString());
                File.WriteAllText(output+"/scene-behaviours.txt",string.Join("\n",Obj.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(c=>c!=null).Select(c=>ScenarioObjectPath(c.transform)+" : "+c.GetType().FullName).OrderBy(s=>s)));
                File.WriteAllText(output+"/audit-scope.txt","Scene SHA256 before="+before+"\nScene SHA256 after="+AuditHash(WideScene)+"\nCaptures="+count+"\n1600x1000 mono Editor renders, current production materials/postprocessing. Current phase proxies and target markers retained; debug labels/routes/reservations hidden.\nExtra inspection cameras are not adopted gameplay cameras. Global late-state toggle only: no animated drop, neck lowering, boss AI, hit states, two-player UX or HMD performance test.\nProjected size is the nominal current 2m marker at camera-forward depth; not final weakpoint art. Collider rays include visible-layer proxies, but omit render-only decoration. Occlusion is not a triangle-visibility verdict.\n");
                if(before!=AuditHash(WideScene))throw new InvalidOperationException("Scenario audit unexpectedly modified saved scene");
                Debug.Log("SCENARIO_READINESS_AUDIT_OK captures="+count+" scene_unchanged="+before);
            }
            finally{EditorSceneManager.OpenScene(WideScene);}
        }

        static string ScenarioObjectPath(Transform t)
        {string result=t.name;while(t.parent!=null){t=t.parent;result=t.name+"/"+result;}return result;}

        static void AppendScenarioCamera(StringBuilder b,string image,Camera c,Vector3 look,string state)
        {
            var p=c.transform.position;
            b.AppendLine(image+","+c.name.Replace(',','_')+","+p.x.ToString(Inv)+","+p.y.ToString(Inv)+","+p.z.ToString(Inv)+","+look.x.ToString(Inv)+","+look.y.ToString(Inv)+","+look.z.ToString(Inv)+","+c.fieldOfView.ToString(Inv)+","+state);
        }
    }
}
