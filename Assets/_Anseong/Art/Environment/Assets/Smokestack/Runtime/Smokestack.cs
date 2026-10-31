using System.Collections;
using UnityEngine;
namespace CoastalFactory {
public sealed class Smokestack:MonoBehaviour {
 [SerializeField,Tooltip("Shell mesh sections, bottom to top. Upper sections become one compound body on impact.")] Rigidbody[] sections;
 [SerializeField] ParticleSystem outlet,rupture,dust;
 [SerializeField,Tooltip("Whether connected plant exhaust is operating.")] bool operating=true;
 Vector3[] positions;Quaternion[] rotations;Transform[] parts;bool broken;GameObject upper;Rigidbody upperBody;int cutIndex;
 public bool IsBroken=>broken;
 public bool IsSingleUpperBody{get{if(!broken||upperBody==null)return false;for(int i=cutIndex;i<parts.Length;i++)if(parts[i].parent!=upper.transform||parts[i].GetComponent<Rigidbody>()!=null)return false;return true;}}
 public void Configure(Rigidbody[] value,ParticleSystem top,ParticleSystem leak,ParticleSystem debris){sections=value;outlet=top;rupture=leak;dust=debris;}
 void Awake(){positions=new Vector3[sections.Length];rotations=new Quaternion[sections.Length];parts=new Transform[sections.Length];for(int i=0;i<sections.Length;i++){parts[i]=sections[i].transform;positions[i]=parts[i].localPosition;rotations[i]=parts[i].localRotation;}if(!operating)outlet.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
 public void ApplyImpact(Vector3 point,Vector3 direction){if(broken||direction.sqrMagnitude<.001f)return;broken=true;cutIndex=Mathf.Clamp(Mathf.FloorToInt((transform.InverseTransformPoint(point).y-5)/12.5f),0,sections.Length-1);StartCoroutine(Break(point,direction.normalized));}
 IEnumerator Break(Vector3 point,Vector3 direction){outlet.Stop(true,ParticleSystemStopBehavior.StopEmitting);dust.transform.position=point;dust.Emit(65);rupture.transform.position=transform.TransformPoint(new Vector3(0,5+cutIndex*12.5f,0));if(operating)rupture.Play();
  upper=new GameObject("Upper_Half_OneRigidBody");upper.transform.SetParent(transform,false);upper.transform.localPosition=positions[cutIndex];
  float mass=0;for(int i=cutIndex;i<parts.Length;i++){mass+=sections[i].mass;sections[i].detectCollisions=false;Destroy(sections[i]);parts[i].SetParent(upper.transform,true);}
  // Remove child rigidbodies first so all convex shell colliders belong to a single compound body.
  yield return null;if(!broken)yield break;upperBody=upper.AddComponent<Rigidbody>();upperBody.mass=mass;upperBody.linearDamping=.15f;upperBody.angularDamping=.25f;upperBody.linearVelocity=direction*7+Vector3.down;upperBody.angularVelocity=Vector3.Cross(Vector3.up,direction)*.55f;
  yield return new WaitForSeconds(3);rupture.Stop(true,ParticleSystemStopBehavior.StopEmitting);yield return new WaitForSeconds(15);upperBody.linearVelocity=Vector3.zero;upperBody.angularVelocity=Vector3.zero;upperBody.isKinematic=true;
 }
 public void ResetStack(){StopAllCoroutines();if(upper)upper.SetActive(false);for(int i=0;i<parts.Length;i++){parts[i].SetParent(transform,false);parts[i].SetLocalPositionAndRotation(positions[i],rotations[i]);parts[i].gameObject.SetActive(true);var body=parts[i].GetComponent<Rigidbody>();if(body==null)body=parts[i].gameObject.AddComponent<Rigidbody>();if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}body.isKinematic=true;body.detectCollisions=true;body.mass=8000;body.linearDamping=.2f;body.angularDamping=.8f;sections[i]=body;}if(upper)Destroy(upper);upper=null;upperBody=null;broken=false;rupture.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);outlet.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(operating)outlet.Play();Physics.SyncTransforms();}
}
}
