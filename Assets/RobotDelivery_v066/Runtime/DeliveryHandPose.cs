using UnityEngine;
[DefaultExecutionOrder(10000)]
public class DeliveryHandPose:MonoBehaviour {
 public Transform[] joints; public Vector3[] positions;public Quaternion[] rotations;
 void LateUpdate(){Apply();}
 public void Apply(){if(joints==null)return;for(int i=0;i<joints.Length;i++)if(joints[i]){joints[i].localPosition=positions[i];joints[i].localRotation=rotations[i];}}
}
