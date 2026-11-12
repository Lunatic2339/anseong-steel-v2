using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
namespace CoastalFactory {
public sealed class FactoryDemo : MonoBehaviour {
 [SerializeField] FactoryBuilding building;
 [SerializeField] Camera view;
 [SerializeField] bool validateOnStart;
 public void Configure(FactoryBuilding target,Camera camera,bool validate){building=target;view=camera;validateOnStart=validate;}
 void Update(){if(building==null)return;var k=Keyboard.current;if(k!=null){if(k.spaceKey.wasPressedThisFrame)building.Collapse();if(k.rKey.wasPressedThisFrame)building.ResetBuilding();}
  var m=Mouse.current;if(m!=null&&m.leftButton.wasPressedThisFrame&&view!=null){if(Physics.Raycast(view.ScreenPointToRay(m.position.ReadValue()),out var hit,1000)&&hit.collider.GetComponentInParent<FactoryBuilding>()==building)building.ApplyImpact(hit.point,55,9);}}
 IEnumerator Start(){if(!validateOnStart)yield break;yield return null;
  if(building.PieceCount<30){Debug.LogError("FACTORY_TEST_FAIL missing pieces");yield break;}
  building.ApplyImpact(building.transform.position+new Vector3(-20,12,-16),60,9);yield return new WaitForSeconds(2);
  int partial=building.DetachedCount;if(partial<1||partial>=building.PieceCount){Debug.LogError("FACTORY_TEST_FAIL localized impact "+partial);yield break;}
  building.ResetBuilding();yield return null;
  if(building.DetachedCount!=0){Debug.LogError("FACTORY_TEST_FAIL reset");yield break;}
  building.Collapse();yield return new WaitForSeconds(4);
  if(building.DetachedCount!=building.PieceCount){Debug.LogError("FACTORY_TEST_FAIL full collapse");yield break;}
  building.ResetBuilding();yield return null;float maxSpeed=0;foreach(var p in building.GetComponentsInChildren<FactoryPiece>())maxSpeed=Mathf.Max(maxSpeed,p.Body.linearVelocity.magnitude);
  Debug.Log("FACTORY_TEST_PASS pieces="+building.PieceCount+" local="+partial+" resetVelocity="+maxSpeed);
 }
}
}
