using UnityEngine;
namespace CoastalFactory {
public sealed class FactoryBuilding:MonoBehaviour {
 [SerializeField,Tooltip("Minimum directional impact strength.")] float minimumImpact=20;
 [SerializeField,Tooltip("Seconds before debris freezes in its damaged resting pose.")] float debrisLifetime=18;
 FactoryPiece[] pieces;float freezeAt=-1,nextSupport;Vector3 lastDirection;
 public int PieceCount=>pieces==null?0:pieces.Length;
 public int DetachedCount{get{int n=0;if(pieces!=null)foreach(var p in pieces)if(p.Detached)n++;return n;}}
 public int DamagedCount{get{int n=0;if(pieces!=null)foreach(var p in pieces)if(p.Damaged)n++;return n;}}
 public int BentCount{get{int n=0;if(pieces!=null)foreach(var p in pieces)if(p.IsBent)n++;return n;}}
 public bool IsCollapsing=>DamagedCount>0;
 void Awake(){pieces=GetComponentsInChildren<FactoryPiece>(true);}
 public bool CollectContact(Vector3 point,float radius,System.Collections.Generic.HashSet<FactoryPiece> hits){bool any=false;foreach(var p in pieces)if(Vector3.Distance(p.GetComponent<BoxCollider>().ClosestPoint(point),point)<=radius){hits.Add(p);any=true;}return any;}
 public float RapidImpactStarted{get;private set;}=-1;
 public void BeginRapidImpact(System.Collections.Generic.HashSet<FactoryPiece> hits,Vector3 point,Vector3 push){
  RapidImpactStarted=Time.time;lastDirection=push.normalized;
  var supports=new System.Collections.Generic.List<FactoryPiece>(hits);
  foreach(var support in supports)if(support.IsSupport)foreach(var p in pieces)if(p.IsOverhead&&Mathf.Abs(p.RestPosition.x-support.RestPosition.x)<=4.1f&&Mathf.Abs(p.RestPosition.z-support.RestPosition.z)<=17)hits.Add(p);
  foreach(var p in hits)p.Crush(lastDirection,.8f,0);
  PullRemainingFrontPanels();DamageAdjacentFrontCorner();
  var ordered=new System.Collections.Generic.List<FactoryPiece>();foreach(var p in pieces)if(p.Damaged)ordered.Add(p);
  ordered.Sort((a,b)=>{int c=Vector3.SqrMagnitude(transform.TransformPoint(a.RestPosition)-point).CompareTo(Vector3.SqrMagnitude(transform.TransformPoint(b.RestPosition)-point));return c!=0?c:string.CompareOrdinal(a.name,b.name);});
  int fast=Mathf.RoundToInt(ordered.Count*.8f),slow=ordered.Count-fast;
  for(int i=0;i<ordered.Count;i++){bool immediate=i<fast;float wait=immediate?.32f*i/Mathf.Max(1,fast-1):.46f+2.86f*(i-fast)/Mathf.Max(1,slow-1);ordered[i].SetTiming(wait,immediate);}
  freezeAt=Time.time+debrisLifetime+3.5f;
 }
 public void PullRemainingFrontPanels(){foreach(var p in pieces)if(!p.Damaged&&p.IsWallPanel&&p.RestPosition.x<-16&&p.RestPosition.z<-15)p.PullInward((24-p.RestPosition.y)*.012f);}
 public void DamageAdjacentFrontCorner(){
  // The robot clips the left/front bay. Only nearby cladding receives secondary damage.
  var candidates=new System.Collections.Generic.List<FactoryPiece>();
  foreach(var p in pieces)if(!p.Damaged&&p.IsWallPanel&&p.RestPosition.y>8&&p.RestPosition.x<-15&&p.RestPosition.z<0)candidates.Add(p);
  Vector3 corner=new Vector3(-24,18,-16);
  candidates.Sort((a,b)=>(a.RestPosition-corner).sqrMagnitude.CompareTo((b.RestPosition-corner).sqrMagnitude));
  for(int i=0;i<Mathf.Min(5,candidates.Count);i++)candidates[i].Dent(transform.TransformDirection(new Vector3(-1,-.15f,-.45f)),i==1||i==3,.12f*i);
 }
 public void ApplyImpact(Vector3 point,float strength,float radius){ApplyDirectionalImpact(point,transform.forward+Vector3.down*.25f,strength,radius);}
 public void ApplyDirectionalImpact(Vector3 point,Vector3 push,float strength,float radius){if(pieces==null||strength<minimumImpact||radius<=0||push.sqrMagnitude<.001f)return;lastDirection=push.normalized;
  foreach(var p in pieces){if(p.Damaged)continue;float distance=Vector3.Distance(p.GetComponent<BoxCollider>().ClosestPoint(point),point);if(distance>radius)continue;float severity=Mathf.Clamp01(strength/90*(1-distance/(radius*1.3f)));p.Crush(lastDirection,severity,distance*.035f);}
  freezeAt=Time.time+debrisLifetime;
 }
 // Local support loss: only overhead members in the damaged column's 8m bay are released.
 void Update(){if(pieces==null)return;if(RapidImpactStarted<0&&Time.time>=nextSupport&&DamagedCount>0){nextSupport=Time.time+.25f;foreach(var support in pieces){if(!support.IsSupport||!support.Damaged)continue;foreach(var p in pieces){if(!p.IsOverhead||p.Damaged)continue;if(Mathf.Abs(p.RestPosition.x-support.RestPosition.x)<=4.1f&&Mathf.Abs(p.RestPosition.z-support.RestPosition.z)<=17)p.Crush(lastDirection+Vector3.down*.5f,.7f,.65f);}}}
  if(freezeAt>0&&Time.time>=freezeAt){foreach(var p in pieces)p.Freeze();freezeAt=-1;}}
 [ContextMenu("Demo / Full demolition (separate from impact)")]
 public void Collapse(){if(!Application.isPlaying||pieces==null)return;lastDirection=transform.forward+Vector3.down*.4f;foreach(var p in pieces)p.Crush(lastDirection,.8f,Mathf.Abs(p.RestPosition.x)*.025f);freezeAt=Time.time+debrisLifetime;}
 public void ResetBuilding(){if(pieces==null)return;foreach(var p in pieces)p.Restore();freezeAt=-1;nextSupport=0;RapidImpactStarted=-1;Physics.SyncTransforms();}
}
}

