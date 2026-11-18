using UnityEngine;

namespace Anseong.Map35
{
    // Timing/occupancy rehearsal only. Constant speed and explicit in-place turns are assumptions.
    public sealed class MapScenarioTransit : MonoBehaviour
    {
        public Transform mech,boss;
        public Vector3 mechReference,bossReference;
        public float elapsed=60,bossSpeed=8,bossLead=6,turnSeconds=2;
        public bool playing;
        public static readonly Vector3[] BossPoints={new Vector3(0,0,300),new Vector3(0,0,310),new Vector3(175,0,310),new Vector3(175,0,405),new Vector3(175,0,465),new Vector3(90,0,465),new Vector3(90,0,555)};
        static readonly Vector3[] BossFacing={Vector3.back,Vector3.right,Vector3.forward,Vector3.forward,Vector3.left,Vector3.back};
        public static readonly Vector3[] MechPoints={new Vector3(0,0,255),new Vector3(0,0,360),new Vector3(0,0,405),new Vector3(90,0,405),new Vector3(90,0,490)};
        public string Status=>"보스 선행6초 · M8 북진은 보스의 마지막 회전이 끝난 뒤 허용. 자세·속도는 공간 검토 가정.";
        void Update(){if(playing)SetTime((elapsed+Time.unscaledDeltaTime)%95);}
        public void SetTime(float value)
        {
            elapsed=value;SampleBoss(elapsed,bossSpeed,bossLead,turnSeconds,out var bp,out var br);
            SampleMech(elapsed,bossSpeed,bossLead,turnSeconds,out var mp,out var mr);
            if(boss!=null)boss.SetPositionAndRotation(bp,br);if(mech!=null)mech.SetPositionAndRotation(mp,mr);
        }
        public static float ReleaseTime(float speed,float lead,float turn)
        {
            float distance=0;for(int i=1;i<BossPoints.Length-1;i++)distance+=Vector3.Distance(BossPoints[i-1],BossPoints[i]);
            // Backstep then south->east, east->north, north->west, west->south. Collinear midpoint has no turn.
            return Mathf.Max(0,distance/speed+4*turn-lead);
        }
        public static void SampleBoss(float time,float speed,float lead,float turn,out Vector3 position,out Quaternion rotation)
        {
            float remaining=Mathf.Max(0,time+lead);Vector3 last=Vector3.back;position=BossPoints[0];rotation=Quaternion.LookRotation(-last);
            for(int i=1;i<BossPoints.Length;i++)
            {
                var direction=BossFacing[i-1];float angle=Vector3.Angle(last,direction);
                if(angle>1)
                {
                    if(remaining<turn){rotation=Quaternion.Slerp(Quaternion.LookRotation(-last),Quaternion.LookRotation(-direction),remaining/turn);return;}
                    remaining-=turn;
                }
                float duration=Vector3.Distance(BossPoints[i-1],BossPoints[i])/speed;rotation=Quaternion.LookRotation(-direction);
                if(remaining<duration){position=Vector3.Lerp(BossPoints[i-1],BossPoints[i],remaining/duration);return;}
                remaining-=duration;position=BossPoints[i];last=direction;
            }
            // Final leg backs into the crane while facing the mech. No unplanned turn under the load.
            rotation=Quaternion.identity;
        }
        public static void SampleMech(float time,float speed,float lead,float turn,out Vector3 position,out Quaternion rotation)
        {
            float remaining=Mathf.Max(0,time);position=MechPoints[0];rotation=Quaternion.identity;float spent=0;
            for(int i=1;i<MechPoints.Length;i++)
            {
                var direction=(MechPoints[i]-MechPoints[i-1]).normalized;
                if(i>1&&Vector3.Angle((MechPoints[i-1]-MechPoints[i-2]).normalized,direction)>1)
                {
                    if(remaining<turn){rotation=Quaternion.Slerp(rotation,Quaternion.LookRotation(direction),remaining/turn);return;}
                    remaining-=turn;spent+=turn;
                }
                if(i==4){float wait=Mathf.Max(0,ReleaseTime(speed,lead,turn)-spent);if(remaining<wait)return;remaining-=wait;spent+=wait;}
                float duration=Vector3.Distance(MechPoints[i-1],MechPoints[i])/4;
                rotation=Quaternion.LookRotation(direction);
                if(remaining<duration){position=Vector3.Lerp(MechPoints[i-1],MechPoints[i],remaining/duration);return;}
                remaining-=duration;spent+=duration;position=MechPoints[i];
            }
        }
    }
}
