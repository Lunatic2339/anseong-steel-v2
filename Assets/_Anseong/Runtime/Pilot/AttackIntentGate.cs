namespace Anseong.Pilot
{
    /// <summary>
    /// 손 하나의 공격 의도. 그립 유지 + 추적 유효일 때만 의도가 있다.
    /// 추적이 끊겼다 돌아오면 그립을 한 번 놓기 전까지 무효다.
    /// <para>
    /// 이 클래스는 "무효"만 정한다. 짧은 끊김에 연계를 초기화하지 않도록(기존 버그 8),
    /// 연계 초기화 여부는 연계 규칙(M1-05)이 <see cref="LostSeconds"/>를 보고 따로 정한다.
    /// </para>
    /// </summary>
    public sealed class AttackIntentGate
    {
        double lostSince;

        /// <summary>이번 갱신의 공격 의도.</summary>
        public bool Intent { get; private set; }

        /// <summary>그립을 한 번 놓아야 다시 공격할 수 있는 상태.</summary>
        public bool AwaitingRelease { get; private set; }

        /// <summary>지금 추적이 끊겨 있다.</summary>
        public bool IsTrackingLost { get; private set; }

        /// <summary>손 상태를 넣고 공격 의도를 돌려준다. time은 초 단위로 늘어나기만 하는 시계.</summary>
        public bool Update(bool tracked, bool gripHeld, double time)
        {
            if (!tracked)
            {
                if (!IsTrackingLost) lostSince = time;
                IsTrackingLost = true;
                AwaitingRelease = true;
                Intent = false;
                return false;
            }

            IsTrackingLost = false;
            if (AwaitingRelease && !gripHeld) AwaitingRelease = false;
            Intent = gripHeld && !AwaitingRelease;
            return Intent;
        }

        /// <summary>그립을 한 번 놓기 전까지 공격을 막는다(예: 보정 단계에서 그립으로 저장한 직후).</summary>
        public void RequireRelease()
        {
            AwaitingRelease = true;
            Intent = false;
        }

        /// <summary>추적이 끊긴 지 몇 초인지. 추적 중이면 0.</summary>
        public double LostSeconds(double now)
        {
            return IsTrackingLost ? now - lostSince : 0d;
        }

        public void Reset()
        {
            Intent = AwaitingRelease = IsTrackingLost = false;
            lostSince = 0d;
        }
    }
}
