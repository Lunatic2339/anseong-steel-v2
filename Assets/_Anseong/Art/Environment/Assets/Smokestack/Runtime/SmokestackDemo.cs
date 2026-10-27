using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
namespace CoastalFactory {
public sealed class SmokestackDemo:MonoBehaviour {
 [SerializeField] Smokestack stack;[SerializeField] bool validate;[SerializeField] string captures;
 public void Configure(Smokestack value,string folder){stack=value;captures=folder;}
 public void EnableValidation(){validate=true;}
 void Update(){if(Keyboard.current==null)return;if(Keyboard.current.spaceKey.wasPressedThisFrame)Hit();if(Keyboard.current.rKey.wasPressedThisFrame)stack.ResetStack();}
 void Hit(){stack.ApplyImpact(stack.transform.TransformPoint(new Vector3(0,28,0)),new Vector3(.7f,0,.7f));}
 IEnumerator Start(){if(!validate)yield break;yield return new WaitForSeconds(1);ScreenCapture.CaptureScreenshot(captures+"/Smokestack_Before.png");yield return null;Hit();yield return new WaitForSeconds(1.5f);ScreenCapture.CaptureScreenshot(captures+"/Smokestack_During.png");yield return new WaitForSeconds(19);ScreenCapture.CaptureScreenshot(captures+"/Smokestack_After.png");yield return null;if(!stack.IsSingleUpperBody)Debug.LogError("SMOKESTACK_TWO_HALVES_FAIL");else Debug.Log("SMOKESTACK_TWO_HALVES_PASS upperRigidBodies=1");stack.ResetStack();yield return null;Debug.Log(stack.IsBroken?"SMOKESTACK_RESET_FAIL":"SMOKESTACK_RESET_PASS");}
}
}


