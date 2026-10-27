using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
namespace CoastalFactory {
// A scale proxy and authored fall trajectory, not the game's ragdoll or structural engineering simulation.
public sealed class RobotFallDemo:MonoBehaviour {
 [SerializeField] FactoryBuilding building; [SerializeField] Transform robot; [SerializeField] bool validate;
 [SerializeField] string captureDirectory;
 readonly Vector3 fallDirection=new Vector3(.5f,0,-.8660254f);
 bool running;
 public void Configure(FactoryBuilding b,Transform r,string folder){building=b;robot=r;captureDirectory=folder;}
 public void SetValidation(){validate=true;}
 void Update(){if(Keyboard.current==null)return;if(Keyboard.current.spaceKey.wasPressedThisFrame&&!running)StartCoroutine(Fall());if(Keyboard.current.rKey.wasPressedThisFrame){StopAllCoroutines();building.ResetBuilding();robot.SetPositionAndRotation(Vector3.zero,Quaternion.identity);running=false;}}
 IEnumerator Start(){if(validate){yield return new WaitForSeconds(.5f);yield return Fall();}}
 public IEnumerator Fall(){if(running)yield break;running=true;building.ResetBuilding();robot.SetPositionAndRotation(Vector3.zero,Quaternion.identity);yield return new WaitForSeconds(.3f);
  Capture("RobotFall_Before");float t=0;bool captured=false,impacted=false;Vector3 axis=Vector3.Cross(Vector3.up,fallDirection);
  while(t<3){t+=Time.deltaTime;float u=Mathf.Clamp01(t/3);float angle=84*u*u;robot.rotation=Quaternion.AngleAxis(angle,axis);robot.position=fallDirection*(3*u*u);
   // Swept body samples: torso and head have finite width, rather than an explosion sphere.
   if(!impacted&&angle>30)for(int h=14;h<=62;h+=4){Vector3 point=robot.TransformPoint(new Vector3(0,h,0));float radius=h<26?3.5f:h<50?6:4.5f;var hits=new System.Collections.Generic.HashSet<FactoryPiece>();if(!building.CollectContact(point,radius,hits))continue;
    // Precompute this authored fall's local damage footprint, then time it from first contact.
    for(float a=30;a<=84;a+=.5f)for(int height=14;height<=62;height+=4){var q=Quaternion.AngleAxis(a,axis);Vector3 sample=q*new Vector3(0,height,0)+fallDirection*(3*a/84);building.CollectContact(sample,height<26?3.5f:height<50?6:4.5f,hits);}
    building.BeginRapidImpact(hits,point,(fallDirection+Vector3.down*.65f).normalized);impacted=true;break;
   }
   if(!captured&&angle>64){Capture("RobotFall_During");captured=true;}
   yield return null;
  }
  yield return new WaitForSeconds(1.6f);Capture("RobotFall_During");yield return new WaitForSeconds(19.4f);Capture("RobotFall_After");
  yield return null;var camera=Camera.main;var cameraPosition=camera.transform.position;var cameraRotation=camera.transform.rotation;camera.transform.position=building.transform.TransformPoint(new Vector3(-60,48,-85));camera.transform.LookAt(building.transform.TransformPoint(new Vector3(-8,12,0)));yield return null;Capture("RobotFall_FoldedDetail");yield return null;camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);
  if(validate){int damaged=building.DamagedCount;int bent=building.BentCount;bool bad=false;int untouched=0,secondary=0,attached=0,dragged=0,fastCount=0;float lastFast=0,firstSlow=float.MaxValue,lastSlow=0;foreach(var p in building.GetComponentsInChildren<FactoryPiece>()){if(p.IsBent&&!p.SecondaryDamage&&(p.FoldInterior<89.9f||p.FoldInterior>120.1f)){bad=true;Debug.LogError("FOLD_ANGLE_BAD "+p.name+" "+p.FoldInterior);}if(p.Damaged){float done=p.CompletedAt-building.RapidImpactStarted;if(p.CompletedAt<0)bad=true;if(p.FastStage){fastCount++;lastFast=Mathf.Max(lastFast,done);}else{firstSlow=Mathf.Min(firstSlow,done);lastSlow=Mathf.Max(lastSlow,done);}}if(p.InwardDragged){dragged++;if(p.Detached||p.transform.localPosition.z-p.RestPosition.z<5.9f){bad=true;Debug.LogError("INWARD_DRAG_FAIL "+p.name);}}if(p.SecondaryDamage){secondary++;if(!p.Detached)attached++;}if(!p.Damaged){untouched++;if(Vector3.Distance(p.transform.localPosition,p.RestPosition)>.01f){bad=true;Debug.LogError("UNTOUCHED_MOVED "+p.name+" "+Vector3.Distance(p.transform.localPosition,p.RestPosition));}}}
   if(fastCount!=Mathf.RoundToInt(damaged*.8f)||lastFast>.65f||lastSlow>3.7f||lastSlow-firstSlow<2.65f){bad=true;Debug.LogError("RAPID_TIMING_FAIL fast="+fastCount+" lastFast="+lastFast+" slowSpan="+(lastSlow-firstSlow));}Debug.Log("RAPID_TIMING fast="+fastCount+" total="+damaged+" lastFast="+lastFast+" slowFirst="+firstSlow+" slowLast="+lastSlow);if(damaged>0&&damaged<building.PieceCount&&bent>0&&untouched>0&&secondary==4&&attached==2&&dragged==3&&!bad)Debug.Log("ROBOT_FALL_TEST_PASS damaged="+damaged+" intact="+untouched+" bent="+bent+" secondary="+secondary+" attachedDents="+attached+" inwardDragged="+dragged+" direction=5oclock");else Debug.LogError("ROBOT_FALL_TEST_FAIL damaged="+damaged+" bent="+bent+" bad="+bad);
  }
  if(validate){building.ResetBuilding();yield return null;if(building.DamagedCount!=0||building.BentCount!=0)Debug.LogError("ROBOT_FALL_RESET_FAIL");else Debug.Log("ROBOT_FALL_RESET_PASS");}
  running=false;
 }
 void Capture(string name){if(string.IsNullOrEmpty(captureDirectory))return;ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureDirectory,name+".png"));}
}
}




