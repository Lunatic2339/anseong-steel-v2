using System;
using Anseong.Pilot;
using UnityEngine;

namespace Anseong.Flow
{
    /// <summary>기체 점검 단계(10_game_scenario.md 장면 1). 숫자가 복제되므로 끝에만 더한다.</summary>
    public enum CheckupStep : byte
    {
        /// <summary>1-1 신경 연결: 보정(뻗기 → 가드).</summary>
        Connect,
        /// <summary>1-2 관절 시험(맨손): 번갈아 세 번 + 트리거 → 철판이 날아감.</summary>
        JointTest,
        /// <summary>1-3 검·방패 장착 + 공 막기 연습(오는 쪽은 오른쪽 HUD에만).</summary>
        ShieldDrill,
        /// <summary>1-4 스캐너·포 장착(설명 한 줄).</summary>
        GunBrief,
        /// <summary>1-5 출격: 둘이 같이 스틱 앞으로.</summary>
        Sortie,
    }

    /// <summary>관절 시험 "좌우 번갈아 세 번" 세기(M4-05). 같은 팔이 연달아 치면 1부터. 순수 클래스.</summary>
    public sealed class AlternationCounter
    {
        PilotSide? last;
        public int Count { get; private set; }
        public bool Done(int needed) => Count >= needed;

        public void Reset()
        {
            last = null;
            Count = 0;
        }

        public int Strike(PilotSide side)
        {
            Count = last.HasValue && last.Value != side ? Count + 1 : 1;
            last = side;
            return Count;
        }
    }

    /// <summary>공 막기 연습(장면 1-3) 조절값. 정비 로봇이 조준(예고) → 던짐 → 날아옴 → 방패 판정 → 쉼을 되풀이한다.</summary>
    [Serializable]
    public class ShieldDrillSettings
    {
        [Min(1), Tooltip("몇 번 막으면 넘어가나(대본 3).")]
        public int blocksNeeded = 3;
        [Min(0), Tooltip("막지 못해도 이만큼 던지면 넘어간다(막히지 않게). 0 = 끝없이.")]
        public int maxThrows = 6;
        [Min(0f), Tooltip("검·방패가 끼워지고 정비 로봇이 올라온 뒤 첫 공까지(초).")]
        public float firstDelay = 4f;
        [Min(1.5f), Tooltip("던지기 전 그쪽으로 돌아 팔을 젖히는 예고 시간(초). 읽을 수 있게 1.5 이상.")]
        public float aimSeconds = 1.8f;
        [Min(0.5f), Tooltip("공이 날아오는 시간(초). 느린 공.")]
        public float flightSeconds = 2f;
        [Min(0.5f), Tooltip("공 사이 쉬는 시간(초).")]
        public float restSeconds = 2f;
        [Min(1), Tooltip("같은 쪽으로 연달아 던지는 최대 횟수.")]
        public int maxSameSide = 2;
    }

    /// <summary>공 막기 연습에서 이번 프레임에 일어난 일.</summary>
    public enum DrillStep
    {
        None,
        /// <summary>새 공: 조준 시작. Side·ReleaseAt·ImpactAt이 정해졌다.</summary>
        Aim,
        /// <summary>공이 닿는 순간: 부른 쪽이 방패 판정을 해서 <see cref="ShieldDrill.Report"/>를 부른다.</summary>
        Impact,
        /// <summary>연습 끝(쉼까지 지남).</summary>
        Done,
    }

    /// <summary>
    /// 공 막기 연습 차례(서버, 순수 클래스). 시각은 모두 서버 시각. 공이 닿는 시각(<see cref="ImpactAt"/>)을 조준 시작 때 정해 두므로
    /// 모든 PC가 같은 시각에 공이 방패에 닿게 그릴 수 있다(예전엔 그림 1.2초·판정 2초로 어긋남).
    /// </summary>
    public sealed class ShieldDrill
    {
        readonly ShieldDrillSettings settings;
        double nextAt;
        bool flying, finished, doneSent;
        int sameRun;

        public ShieldDrill(ShieldDrillSettings settings) => this.settings = settings ?? new ShieldDrillSettings();

        public PilotSide Side { get; private set; }
        public double AimAt { get; private set; }
        public double ReleaseAt { get; private set; }
        public double ImpactAt { get; private set; }
        public int Blocks { get; private set; }
        public int Throws { get; private set; }
        public bool Finished => finished;
        /// <summary>공이 조준·비행 중(아직 판정 전).</summary>
        public bool InFlight => flying;

        public void Begin(double now)
        {
            Blocks = Throws = sameRun = 0;
            flying = finished = doneSent = false;
            nextAt = now + settings.firstDelay;
        }

        /// <param name="random01">0~1 난수(0.5 미만이면 왼쪽). 같은 쪽이 너무 이어지면 반대쪽으로 바꾼다.</param>
        public DrillStep Update(double now, float random01)
        {
            if (finished)
            {
                if (doneSent || now < nextAt) return DrillStep.None;
                doneSent = true;
                return DrillStep.Done;
            }
            if (flying) return now >= ImpactAt ? DrillStep.Impact : DrillStep.None;
            if (now < nextAt) return DrillStep.None;

            var side = random01 < 0.5f ? PilotSide.Left : PilotSide.Right;
            if (Throws > 0 && side == Side && sameRun >= Mathf.Max(1, settings.maxSameSide))
                side = side == PilotSide.Left ? PilotSide.Right : PilotSide.Left;
            sameRun = Throws > 0 && side == Side ? sameRun + 1 : 1;
            Side = side;
            Throws++;
            flying = true;
            AimAt = now;
            ReleaseAt = now + settings.aimSeconds;
            ImpactAt = ReleaseAt + settings.flightSeconds;
            return DrillStep.Aim;
        }

        /// <summary>닿는 순간의 판정 결과를 넣는다. 연습이 끝났으면 true(쉼 뒤 <see cref="DrillStep.Done"/>이 한 번 나온다).</summary>
        public bool Report(bool blocked, double now)
        {
            if (!flying) return finished;
            flying = false;
            if (blocked) Blocks++;
            nextAt = now + settings.restSeconds;
            finished = Blocks >= settings.blocksNeeded || (settings.maxThrows > 0 && Throws >= settings.maxThrows);
            return finished;
        }
    }
}
