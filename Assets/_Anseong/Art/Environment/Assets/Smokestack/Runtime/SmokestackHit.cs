using UnityEngine;
namespace CoastalFactory {
public sealed class SmokestackHit:MonoBehaviour {
 Smokestack stack;void Awake(){stack=GetComponentInParent<Smokestack>();}
 void OnCollisionEnter(Collision c){if(stack==null||stack.IsBroken||c.contactCount==0||c.relativeVelocity.magnitude<8||c.collider.GetComponentInParent<Smokestack>()==stack)return;stack.ApplyImpact(c.GetContact(0).point,-c.relativeVelocity);}
}
}

