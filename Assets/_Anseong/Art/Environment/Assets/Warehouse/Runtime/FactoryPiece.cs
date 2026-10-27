using System.Collections.Generic;
using UnityEngine;
namespace CoastalFactory {
[RequireComponent(typeof(Rigidbody),typeof(BoxCollider))]
public sealed class FactoryPiece:MonoBehaviour {
 Rigidbody body; FactoryBuilding owner; BoxCollider box; MeshFilter filter; Mesh original,bent;
 Vector3 startPosition,startScale; Quaternion startRotation; Vector3[] vertices; bool panel,roof,scheduled;
 Vector3 direction,axis; float delay,elapsed,angle; GameObject foldedCollision; bool finished,release=true;
 public bool SecondaryDamage{get;private set;}
 public bool InwardDragged{get;private set;}
 float duration=.85f;
 public bool FastStage{get;private set;}
 public float CompletedAt{get;private set;}=-1;
 public void SetTiming(float wait,bool fast){delay=wait;duration=.14f;FastStage=fast;}
 public bool IsWallPanel=>panel&&!roof;
 public bool Detached{get;private set;} public bool Damaged=>scheduled||Detached;
 public bool IsBent=>bent!=null; public float FoldInterior=>bent==null?180:180-angle;
 public Rigidbody Body=>body; public Vector3 RestPosition=>startPosition;
 public bool IsSupport=>name.StartsWith("Column");
 public bool IsOverhead=>name.StartsWith("Roof")||name.StartsWith("Gable")||name.StartsWith("Longitudinal");
 void Awake(){body=GetComponent<Rigidbody>();box=GetComponent<BoxCollider>();filter=GetComponent<MeshFilter>();original=filter.sharedMesh;owner=GetComponentInParent<FactoryBuilding>();startPosition=transform.localPosition;startRotation=transform.localRotation;startScale=transform.localScale;roof=name.StartsWith("RoofPanel");panel=roof||name.Contains("Wall")||name.StartsWith("Loading_Shutter");body.linearDamping=.4f;body.angularDamping=1.3f;body.maxAngularVelocity=2;}
 public void Crush(Vector3 push,float severity,float wait){if(Damaged)return;scheduled=true;direction=push.normalized;delay=wait;elapsed=0;angle=Mathf.Lerp(60,90,Mathf.Clamp01(severity));if(panel)PrepareBend();}
 public void Dent(Vector3 push,bool detach,float wait){if(Damaged||!IsWallPanel)return;scheduled=true;SecondaryDamage=true;release=detach;direction=push.normalized;delay=wait;elapsed=0;angle=detach?38:22;PrepareBend();}
 public void PullInward(float wait){if(Damaged||!IsWallPanel)return;scheduled=true;InwardDragged=true;release=false;direction=transform.parent.TransformDirection(new Vector3(0,-.4f,1)).normalized;delay=wait;elapsed=0;angle=70;PrepareBend();}
 void PrepareBend(){var list=new List<Vector3>();var tris=new List<int>();var src=original.vertices;var ids=original.triangles;
  for(int i=0;i<ids.Length;i+=3)Subdivide(Vector3.Scale(src[ids[i]],startScale),Vector3.Scale(src[ids[i+1]],startScale),Vector3.Scale(src[ids[i+2]],startScale),2,list,tris);
  vertices=list.ToArray();bent=new Mesh{name="RuntimeFold_"+name};bent.SetVertices(list);bent.SetTriangles(tris,0);bent.RecalculateNormals();filter.sharedMesh=bent;transform.localScale=Vector3.one;
  box.size=Vector3.Scale(box.size,startScale);box.center=Vector3.Scale(box.center,startScale);
  axis=(roof||startScale.z<startScale.x)?Vector3.right:Vector3.forward;
  var normal=roof?Vector3.up:(startScale.z<startScale.x?Vector3.forward:Vector3.right);
  float sign=Vector3.Dot(transform.InverseTransformDirection(direction),normal)>=0?1:-1;
  if(roof)sign=-sign;else if(axis==Vector3.forward)sign=-sign;axis*=sign;
 }
 static void Subdivide(Vector3 a,Vector3 b,Vector3 c,int depth,List<Vector3> v,List<int> t){if(depth==0){int n=v.Count;v.Add(a);v.Add(b);v.Add(c);t.Add(n);t.Add(n+1);t.Add(n+2);return;}var ab=(a+b)*.5f;var bc=(b+c)*.5f;var ca=(c+a)*.5f;Subdivide(a,ab,ca,depth-1,v,t);Subdivide(ab,b,bc,depth-1,v,t);Subdivide(ca,bc,c,depth-1,v,t);Subdivide(ab,bc,ca,depth-1,v,t);}
 Vector3 Fold(Vector3 v,float degrees){float coordinate=roof?v.z:v.y;return coordinate>0?Quaternion.AngleAxis(degrees,axis)*v:v;}
 void Update(){if(!scheduled||finished)return;elapsed+=Time.deltaTime;if(elapsed<delay)return;float t=Mathf.Clamp01((elapsed-delay)/duration);float smooth=t*t*(3-2*t);
  if(InwardDragged)transform.localPosition=startPosition+new Vector3(0,-3,6)*smooth;
  if(bent!=null){var deformed=new Vector3[vertices.Length];for(int i=0;i<deformed.Length;i++)deformed[i]=Fold(vertices[i],angle*smooth);bent.vertices=deformed;bent.RecalculateNormals();bent.RecalculateBounds();}
  if(t>=1){finished=true;CompletedAt=Time.time;if(bent!=null)FoldedColliders();if(release){Detached=true;body.isKinematic=false;body.linearVelocity=direction*(SecondaryDamage?5:3.5f)+Vector3.down*1.2f;body.angularVelocity=Vector3.Cross(Vector3.up,direction)*.35f;body.WakeUp();}}}
 void FoldedColliders(){box.enabled=false;foldedCollision=new GameObject("FoldedPanel_Collision");foldedCollision.transform.SetParent(transform,false);Vector3 size=new Vector3(Mathf.Abs(startScale.x),Mathf.Abs(startScale.y),Mathf.Abs(startScale.z));
  for(int half=0;half<2;half++){var g=new GameObject("Half_"+half);g.transform.SetParent(foldedCollision.transform,false);Vector3 halfSize=size;Vector3 center=Vector3.zero;if(roof){halfSize.z*=.5f;center.z=size.z*.25f*(half==0?-1:1);}else{halfSize.y*=.5f;center.y=size.y*.25f*(half==0?-1:1);}if(half==1){g.transform.localRotation=Quaternion.AngleAxis(angle,axis);center=Fold(center,angle);}g.transform.localPosition=center;g.AddComponent<BoxCollider>().size=halfSize;}}
 public void Restore(){if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}body.isKinematic=true;transform.localPosition=startPosition;transform.localRotation=startRotation;transform.localScale=startScale;filter.sharedMesh=original;if(bent)Destroy(bent);bent=null;vertices=null;if(foldedCollision){foldedCollision.SetActive(false);Destroy(foldedCollision);}box.enabled=true;box.center=Vector3.zero;box.size=Vector3.one;Detached=false;scheduled=false;finished=false;release=true;SecondaryDamage=false;InwardDragged=false;duration=.85f;FastStage=false;CompletedAt=-1;}
 public void Freeze(){if(Detached&&!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.isKinematic=true;}}
 // Kinematic pose can accumulate small roundoff under a rotated parent. Keep untouched modules at their exact authored pose.
 void LateUpdate(){if(!Damaged){transform.localPosition=startPosition;transform.localRotation=startRotation;}}
 void OnCollisionEnter(Collision c){if(Damaged||owner==null||c.contactCount==0||c.collider.GetComponentInParent<FactoryBuilding>()==owner)return;if(c.relativeVelocity.magnitude>=8)owner.ApplyDirectionalImpact(c.GetContact(0).point,-c.relativeVelocity.normalized,c.relativeVelocity.magnitude*5,8);}
 void OnDestroy(){if(bent)Destroy(bent);}
}
}



