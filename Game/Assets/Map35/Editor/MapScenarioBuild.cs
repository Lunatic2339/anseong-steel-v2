using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Obj=UnityEngine.Object;

namespace Anseong.Map35.Editor
{
    public static partial class Map35Build
    {
        static string ScenarioOutput=>Path.GetFullPath("../docs/validation/map-scenario-improvement");

        [MenuItem("Anseong/Map35/Apply Scenario Spatial Improvements")]
        public static void ApplyScenarioSpatialImprovements()
        {
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();Directory.CreateDirectory(ScenarioOutput);
            ApplyScenarioEnvironmentCorrections();BuildScenarioSpatialRehearsal();
            ValidateFiniteMapGeometry();AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
            CaptureAndValidateScenario();
            EditorSceneManager.OpenScene(WideScene);Debug.Log("SCENARIO_SPATIAL_APPLIED_OK");
        }

        static void BuildScenarioSpatialRehearsal()
        {
            var prior=Obj.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="13 Scenario spatial rehearsal");
            if(prior!=null)Obj.DestroyImmediate(prior.gameObject);
            foreach(var c in viewer.views.Where(c=>c!=null&&c.name.StartsWith("SCENARIO ")).ToArray())Obj.DestroyImmediate(c.gameObject);
            viewer.views=viewer.views.Where(c=>c!=null).ToArray();
            var root=Group("13 Scenario spatial rehearsal");var preview=root.gameObject.AddComponent<MapScenarioPreview>();preview.viewer=viewer;viewer.scenario=preview;
            var hidden=new List<GameObject>{proxies.gameObject,viewer.coil.gameObject,viewer.lateCoil};
            string[] obsolete={"T1 front shell","T2 west nerve","T3 low neck","E01 control emitter","P02 crane latch","Coil east edge","I6 final weakpoint"};
            foreach(Transform t in details)if(obsolete.Contains(t.name))hidden.Add(t.gameObject);
            preview.hiddenLegacy=hidden.ToArray();
            preview.firstCamera=viewer.views.Length;var views=viewer.views.ToList();
            foreach(var item in new[]{(3,"VR"),(6,"PC3 Drone"),(10,"PC4 Overview"),(11,"PC4 Detail")})
            {
                var c=Obj.Instantiate(viewer.views[item.Item1],cameras);c.name="SCENARIO "+item.Item2;c.enabled=false;
                c.cullingMask|=(1<<11)|(1<<12);if(item.Item1==3)c.cullingMask&=~((1<<8)|(1<<9)|(1<<10));
                else c.cullingMask|=(1<<8)|(1<<9)|(1<<10);views.Add(c);
            }
            viewer.views=views.ToArray();
            var common=Group("Shared scenario installations",root);var e01=BuildScenarioE01(common);var e02=BuildScenarioE02(common);
            var e01Target=e01.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Target E01");
            var e02Target=e02.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Target E02");
            foreach(var tm in Existing("B07").GetComponentsInChildren<TextMesh>(true))
                if(tm.text.Contains("C-07 RELEASE"))tm.text="LOCAL SERVICE";
            var steps=new List<ScenarioSpatialStep>();
            var z1=ScenarioStep(root,"z1-plate","01 출격판 · 하중과 해제",new Vector3(0,0,0),
                "접촉면을 밀어 하중 해소","동쪽 노출 핀 조준","하중 상태 확인 후 해제 대상 선택");
            var z1targets=new[]{ScenarioAnchor(z1.root.transform,"접촉면",new Vector3(0,24,7.3f)),ScenarioAnchor(z1.root.transform,"해제 핀",new Vector3(9.2f,26,8))};
            z1.targets=z1targets;z1.targetNames=new[]{"접촉면","해제 핀"};
            z1.views=new[]{SV(new Vector3(0,29.75f,0),new Vector3(0,24,8),70),SV(new Vector3(16,30,8),new Vector3(9.2f,26,8)),SV(new Vector3(-18,32,-35),new Vector3(0,24,8)),SV(new Vector3(-30,39,20),new Vector3(-5.4f,26,8),50)};steps.Add(z1);
            var fight=ScenarioStep(root,"z1-fight","02 첫 교전 · 전방의 두 위협",new Vector3(0,0,65),"정면 사격과 오른쪽 전방 방어","좌우 각도 선택·동료 위협 제거","K1에서 두 적 비교·공격 중단 선택");
            var swarm=BuildScenarioFirstEncounter(fight.root.transform);AssignScenarioTargets(fight,swarm,new[]{"Target E1","Target E2"},new[]{"침식체 1","침식체 2"});
            fight.views=new[]{SV(new Vector3(0,29.75f,65),new Vector3(7,30,87),70),SV(new Vector3(35,32,65),new Vector3(5,30,85)),SV(new Vector3(-95,80,95),new Vector3(5,29,80)),SV(new Vector3(-45,48,103),new Vector3(9,30,82),50)};steps.Add(fight);
            foreach(var entry in new[]{("z2-arrival","03 보스 전신 등장","arrival"),("z2-defend","04 앞발 예고 · 방어","defend"),("z2-open","05 갑각 개방 · 측면 공략","shell_open"),("z2-neck","06 목 노출 · 합동 타격","neck")})
            {
                bool arrival=entry.Item3=="arrival",neck=entry.Item3=="neck";var mp=new Vector3(0,0,arrival?180:255);
                var s=ScenarioStep(root,entry.Item1,entry.Item2,mp,neck?"앞쪽 목 노출면 타격":"정면 갑각/앞발 예고 확인",neck?"노출 유지·메카 타격 관찰":"서측 신경낭 사격선 확보",neck?"붕괴·노출 상태 확인":"전체 위협 또는 측면 신경낭 선택");
                var boss=BuildScenarioBossPose(entry.Item1,new Vector3(0,0,300),entry.Item3,false,s.root.transform);
                AssignScenarioTargets(s,boss,new[]{"Target T1","Target T2","Target Neck"},new[]{"전면 갑각","서측 신경낭","목 노출면"});
                if(entry.Item3=="shell_open"||neck){s.targets=s.targets.Concat(new[]{e01Target}).ToArray();s.targetNames=s.targetNames.Concat(new[]{"E01 교란 설비"}).ToArray();}
                s.views=new[]{SV(mp+Vector3.up*29.75f,arrival?new Vector3(0,28,300):new Vector3(-7,32,290),70),SV(new Vector3(-50,42,300),new Vector3(-20,34,283)),SV(new Vector3(-180,125,165),new Vector3(0,25,285),60),SV(new Vector3(-90,55,300),new Vector3(-20,34,283),50)};
                steps.Add(s);
            }
            var transit=ScenarioStep(root,"transit","07 연결 이동 · 교차 점유 확인",new Vector3(0,0,255),"회전 안내를 따라 북쪽·동쪽 진행","외곽으로 이동하며 크레인 준비","K3 미리보기·교차 점유와 준비 상태 관찰");
            var transitMech=transit.root.transform.Find("Mech spatial footprint");
            var transitBoss=BuildScenarioBossPose("Moving boss spatial envelope",new Vector3(0,0,300),"arrival",false,transit.root.transform);
            var timeline=transit.root.AddComponent<MapScenarioTransit>();timeline.mech=transitMech;timeline.boss=transitBoss;preview.transit=timeline;
            transit.views=new[]{SV(new Vector3(90,29.75f,405),new Vector3(90,25,490),70),SV(new Vector3(-30,60,405),new Vector3(130,30,460)),SV(new Vector3(-100,190,360),new Vector3(95,0,450),65),SV(new Vector3(260,140,410),new Vector3(100,20,460),60)};
            transit.targets=new Transform[0];transit.targetNames=new string[0];steps.Add(transit);
            var craneSteps=new[]{
                ("z3-prepare","08 크레인 · 준비 전","prepare","arrival"),
                ("z3-ready","09 크레인 · 준비 완료","ready","arrival"),
                ("z3-windup","10 공격 방향 확정","windup","windup"),
                ("z3-evade","11 선대피 · 낙하 기회","evaded","recovery"),
                ("z3-hit","12 코일 명중 · 서쪽 정착","success","hit_follow"),
                ("z3-miss","13 빗나감 · 중앙 정착","miss","miss_follow"),
                ("z3-hit-follow","14 성공 뒤 후속 공략","success_follow","hit_follow"),
                ("z3-miss-follow","15 실패 뒤 일반 공략","miss_follow","miss_follow")};
            foreach(var e in craneSteps)
            {
                bool evade=e.Item3=="evaded"||e.Item3=="success"||e.Item3=="miss";
                bool follow=e.Item3.EndsWith("follow");var mp=new Vector3(evade?72:90,0,504);
                var s=ScenarioStep(root,e.Item1,e.Item2,mp,evade?"서쪽 대피 공간에서 낙하 결과 확인":follow?"손상된 목을 다시 공략":"남쪽 유도점에서 전면 공격을 유도",
                    follow?"동측에 이어진 신경 조직 사격":"준비 핀과 실행 면을 구별해 조준",
                    follow?"E02로 공격 중단 또는 노출 기회 지원":"전체 영상으로 안전 확인·근접 영상으로 장치 상태 확인");
                var boss=BuildScenarioBossPose(e.Item1,new Vector3(90,0,555),e.Item4,true,s.root.transform);
                var crane=BuildScenarioCraneState(e.Item1+" crane",e.Item3,s.root.transform);
                var list=new List<Transform>();var names=new List<string>();
                foreach(var pair in new[]{("Target CraneLatch","운송 고정 핀"),("Target CraneRelease","낙하 실행 면"),("Target Evacuation","서쪽 대피 구획")})
                if(!follow)
                {var t=crane.GetComponentsInChildren<Transform>(true).FirstOrDefault(a=>a.name==pair.Item1);if(t!=null){list.Add(t);names.Add(pair.Item2);}}
                if(e.Item4=="hit_follow"||e.Item4=="miss_follow"||e.Item4=="recovery")
                {list.Add(boss.Find("Target T2"));names.Add("동측 후속 신경낭");list.Add(boss.Find("Target Neck"));names.Add("목 노출면");}
                if(follow){list.Add(e02Target);names.Add("E02 교란 설비");}
                s.targets=list.ToArray();s.targetNames=names.ToArray();
                var drone=follow?new Vector3(165,42,545):new Vector3(165,54,555);
                s.views=new[]{SV(mp+Vector3.up*29.75f,new Vector3(90,42,555),70),SV(drone,follow?new Vector3(110,34,538):new Vector3(147.85f,48.1f,559)),SV(new Vector3(-130,145,410),new Vector3(90,25,545),60),SV(follow?new Vector3(210,55,590):new Vector3(175,60,555),follow?new Vector3(145,35,566):new Vector3(145,48,557),55)};
                steps.Add(s);
            }
            preview.steps=steps.ToArray();preview.SelectStep(0);preview.SelectRole(0);
            var bossRoute=routes.GetComponentsInChildren<LineRenderer>(true).FirstOrDefault(l=>l.name=="Boss route");
            if(bossRoute!=null){bossRoute.positionCount=WideBoss.Length;bossRoute.SetPositions(WideBoss.Select(p=>p+Vector3.up*.8f).ToArray());}
            foreach(var t in root.GetComponentsInChildren<Transform>(true))GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
            foreach(var c in root.GetComponentsInChildren<Collider>(true))if(c.transform.IsChildOf(common))c.enabled=false;
            Directory.CreateDirectory(ScenarioOutput);File.WriteAllText(ScenarioOutput+"/implementation-scope.txt","15 spatial-review steps; four role cameras reused per step. Authored geometry, targets and state selection. No autonomous combat, network game or HMD acceptance.\n");
        }

        static ScenarioSpatialStep ScenarioStep(Transform parent,string id,string title,Vector3 mechPosition,string vr,string drone,string control)
        {
            var root=Group("Step "+id,parent);var anchor=Group("Mech spatial footprint",root);anchor.position=mechPosition;
            var source=proxies.Cast<Transform>().First(t=>t.name=="Mech Z2");var clone=Obj.Instantiate(source.gameObject,anchor,true);clone.name="35m folded mech review";
            clone.transform.position+=mechPosition-new Vector3(0,0,255);clone.SetActive(true);
            return new ScenarioSpatialStep{id=id,title=title,root=root.gameObject,vrTask=vr,droneTask=drone,controlTask=control};
        }
        static Transform ScenarioAnchor(Transform parent,string name,Vector3 p){var t=Group(name,parent);t.position=p;return t;}
        static ScenarioView SV(Vector3 p,Vector3 look,float fov=65)=>new ScenarioView{position=p,look=look,fov=fov};
        static void AssignScenarioTargets(ScenarioSpatialStep step,Transform root,string[] ids,string[] names)
        {
            var list=new List<Transform>();var labels=new List<string>();
            for(int i=0;i<ids.Length;i++){var t=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(a=>a.name==ids[i]);if(t!=null&&t.gameObject.activeSelf){list.Add(t);labels.Add(names[i]);}}
            step.targets=list.ToArray();step.targetNames=labels.ToArray();
        }

        [MenuItem("Anseong/Map35/Capture And Validate Scenario")]
        public static void CaptureAndValidateScenario()
        {
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();WarmSceneParticles();Directory.CreateDirectory(ScenarioOutput);
            var preview=viewer.scenario;var report=new StringBuilder("step,role,target,in_frame,height_2m_px,first_collider,distance_to_target\n");
            var anchors=new StringBuilder("step,target,x,y,z\n");int captures=0;
            for(int s=0;s<preview.steps.Length;s++)
            {
                preview.SelectStep(s);Physics.SyncTransforms();
                foreach(var t in preview.Current.targets){var p=t.position;anchors.AppendLine(preview.Current.id+","+t.name+","+p.x.ToString(Inv)+","+p.y.ToString(Inv)+","+p.z.ToString(Inv));}
                for(int role=0;role<4;role++)
                {
                    preview.SelectRole(role);var c=viewer.views[preview.firstCamera+role];SaveWearCamera(c,ScenarioOutput+"/"+preview.Current.id+"-role"+role+".png");captures++;
                    foreach(var t in preview.Current.targets)
                    {
                        if(!t.gameObject.activeInHierarchy)continue;var p=c.WorldToViewportPoint(t.position);var ray=t.position-c.transform.position;
                        var hits=Physics.RaycastAll(c.transform.position,ray.normalized,ray.magnitude+.1f,c.cullingMask,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance).ToArray();
                        var hit=hits.FirstOrDefault();var blocker=hit.collider==null?"":ScenarioObjectPath(hit.collider.transform);
                        float gap=hit.collider==null?0:ray.magnitude-hit.distance;
                        report.AppendLine(preview.Current.id+","+role+","+t.name+","+(p.z>0&&p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1)+","+(p.z>0?1000/Mathf.Tan(c.fieldOfView*Mathf.Deg2Rad/2)/p.z:0).ToString("F2",Inv)+","+blocker.Replace(',','_')+","+gap.ToString("F3",Inv));
                    }
                }
            }
            File.WriteAllText(ScenarioOutput+"/role-target-visibility.csv",report.ToString());File.WriteAllText(ScenarioOutput+"/actual-anchors.csv",anchors.ToString());
            preview.SelectStep(0);ValidateScenarioSpatialChecks(preview);
            foreach(int i in new[]{9,28,35,37,40}){viewer.Select(i);SaveWearCamera(viewer.views[i],ScenarioOutput+"/environment-"+i.ToString("00")+".png");}
            // Extra travel views expose the previous floating B09 pipes, without debug overlays.
            var temp=Obj.Instantiate(viewer.views[14]);temp.enabled=false;temp.transform.position=new Vector3(0,29.75f,360);temp.transform.LookAt(new Vector3(0,22,405));
            SaveWearCamera(temp,ScenarioOutput+"/M6-connector-corrected.png");Obj.DestroyImmediate(temp.gameObject);
            var vehicleCamera=Obj.Instantiate(viewer.views[37]);vehicleCamera.enabled=false;vehicleCamera.transform.position=new Vector3(-57,2.4f,47);vehicleCamera.transform.LookAt(new Vector3(-64,2,58));
            SaveWearCamera(vehicleCamera,ScenarioOutput+"/B02-carrier-contact.png");Obj.DestroyImmediate(vehicleCamera.gameObject);
            preview.SelectStep(Array.FindIndex(preview.steps,s=>s.id=="transit"));preview.SelectRole(2);
            foreach(float time in new[]{0f,30f,60f,85f}){preview.transit.SetTime(time);SaveWearCamera(viewer.views[preview.firstCamera+2],ScenarioOutput+"/transit-"+time.ToString("00",Inv)+".png");}
            var motion=new StringBuilder("time,mech_x,mech_z,mech_yaw,boss_x,boss_z,boss_heading\n");
            for(int sample=-60;sample<=950;sample++)
            {
                float time=sample/10f;preview.transit.SetTime(time);var mt=preview.transit.mech;var bt=preview.transit.boss;
                motion.AppendLine(string.Join(",",new[]{time,mt.position.x,mt.position.z,mt.eulerAngles.y,bt.position.x,bt.position.z,Mathf.Repeat(bt.eulerAngles.y+180,360)}.Select(v=>v.ToString("F5",Inv))));
            }
            File.WriteAllText(ScenarioOutput+"/transit-unity-samples.csv",motion.ToString());
            File.WriteAllText(ScenarioOutput+"/capture-scope.txt","Role images="+captures+" plus environment7 and transit4. Mono Editor and native player camera renders, no IMGUI/HUD. Selection preservation checked through runtime API, not human mouse input. Spatial states only, not animated co-op gameplay.\n");
            EditorSceneManager.OpenScene(WideScene);Debug.Log("SCENARIO_CAPTURE_VALIDATION_OK captures="+captures);
        }

        static void ValidateScenarioSpatialChecks(MapScenarioPreview preview)
        {
            var errors=new List<string>();var summary=new StringBuilder();
            int orphan=Obj.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(t=>t.name=="B09 pipe");
            summary.AppendLine("Orphan B09 pipe count="+orphan);if(orphan!=0)errors.Add("Orphan B09 pipe remains");
            foreach(var step in preview.steps)
            {
                preview.SelectStep(Array.IndexOf(preview.steps,step));Physics.SyncTransforms();
                if(preview.steps.Count(s=>s.root.activeSelf)!=1)errors.Add("Multiple spatial steps active");
                if(step.id.EndsWith("follow"))
                {
                    var coil=step.root.GetComponentsInChildren<Renderer>().Where(x=>x.name.Contains("rolled_steel")||x.name.Contains("coil")).ToArray();
                    summary.AppendLine(step.id+": active rendered coil surfaces="+coil.Length);
                }
                if(step.id=="z2-neck"||step.id.EndsWith("follow"))
                {
                    var neck=step.targets.FirstOrDefault(t=>t.name=="Target Neck");var c=viewer.views[preview.firstCamera];var p=c.WorldToViewportPoint(neck.position);
                    bool visible=p.z>0&&p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1;summary.AppendLine(step.id+" neck in VR frame="+visible+" viewport="+p);
                    if(!visible)errors.Add(step.id+" neck outside camera");
                }
            }
            ValidateScenarioGeometry(preview,errors,summary);
            preview.SelectStep(0);ValidateFiniteMapGeometry();AuditVisualUpgrade();
            File.WriteAllText(ScenarioOutput+"/spatial-validation.txt",summary+"Errors="+errors.Count+"\n"+string.Join("\n",errors));
            if(errors.Count>0)throw new InvalidOperationException(string.Join(";",errors));
        }

        [MenuItem("Anseong/Map35/Bake Build Scenario Improvements")]
        public static void BakeBuildScenarioImprovements()
        {
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();viewer.scenario.SelectStep(0);
            PlayerSettings.SetStaticBatchingForPlatform(BuildTarget.StandaloneWindows64,true);PlayerSettings.SetDynamicBatchingForPlatform(BuildTarget.StandaloneWindows64,false);
            AlignAcquiredGroundContact();ValidateFiniteMapGeometry();
            if(GameObject.Find("B02 maintenance service yard")!=null)ValidateMaintenanceAccess();
            PrepareZ1Lighting();AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
            Debug.Log("Z1_INDIRECT_BAKE_START");bool baked=Lightmapping.Bake();
            if(!baked||LightmapSettings.lightmaps.Length==0)throw new InvalidOperationException("Z1 indirect lighting produced no lightmaps");
            Debug.Log("Z1_INDIRECT_BAKE_OK maps="+LightmapSettings.lightmaps.Length);
            WarmSceneParticles();BakeLocalReflections();viewer.scenario.SelectStep(0);AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);ValidateWide();AuditVisualUpgrade();
            CaptureAndValidateScenario();
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();viewer.scenario.SelectStep(0);viewer.scenario.SelectRole(0);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),WideScene);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{WideScene},locationPathName="../Builds/MapWide/AnseongMapWide.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Scenario viewer final build failed");
            File.WriteAllText(ScenarioOutput+"/final-scene-sha256.txt",AuditHash(WideScene));Debug.Log("SCENARIO_FINAL_BUILD_OK steps="+viewer.scenario.steps.Length);
        }

        [MenuItem("Anseong/Map35/Build Scenario Viewer Only")]
        public static void BuildScenarioViewerOnly()
        {
            EditorSceneManager.OpenScene(WideScene);ReconnectWideScene();viewer.scenario.SelectStep(0);viewer.scenario.SelectRole(0);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{WideScene},locationPathName="../Builds/MapWide/AnseongMapWide.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Scenario viewer build failed");
            Debug.Log("SCENARIO_VIEWER_BUILD_OK steps="+viewer.scenario.steps.Length);
        }
    }
}
