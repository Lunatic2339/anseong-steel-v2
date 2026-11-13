using System;
using UnityEngine;

namespace Anseong.Combat
{
    [Serializable]
    public class GrabSlashSettings
    {
        [Tooltip("왼팔을 정면으로 뻗고 그립을 쥔 채 이만큼(초) 버티면 붙잡는다. 잽은 바로 거두므로 잡기로 안 잡힌다.")]
        [Range(0.1f, 1.5f)] public float grabSeconds = 0.35f;

        [Tooltip("붙잡고 버틸 수 있는 시간(초). 지나면 보스가 뿌리친다.")]
        [Range(1f, 8f)] public float maxHoldSeconds = 3.5f;

        [Tooltip("한 번 쓴 뒤 다시 쓸 수 있기까지(초). 빈틈마다 쓰면 평타가 의미 없어진다.")]
        [Range(0f, 60f)] public float cooldownSeconds = 18f;

        [Tooltip("붙잡힌 보스를 오른팔로 벨 때 추가 피해(보스 HP %). 평타 피해에 더한다.")]
        [Range(0f, 30f)] public float slashDamage = 9f;

        [Tooltip("같은 구간에서 이만큼 당한 뒤로는 보스가 익숙해져 빨리 뿌리친다(같은 수가 계속 통하지 않게).")]
        [Range(1, 10)] public int learnAfter = 2;

        [Tooltip("익숙해진 보스가 붙잡혀 버티는 시간 비율(1 = 그대로).")]
        [Range(0.1f, 1f)] public float learnedHoldScale = 0.35f;

        [Tooltip("베인 뒤 보스가 휘청이는 시간(초).")]
        [Range(0.2f, 4f)] public float reelSeconds = 1.4f;

        [Tooltip("휘청 뒤 이어지는 빈틈(초).")]
        [Range(0f, 5f)] public float openingAfter = 2f;
    }

    public enum GrabStep
    {
        None,
        /// <summary>왼팔이 보스를 붙잡았다.</summary>
        Started,
        /// <summary>왼팔이 놓았거나 보스가 잡을 수 없는 상태가 됐다.</summary>
        Broken,
        /// <summary>버틸 수 있는 시간이 지나 보스가 뿌리쳤다.</summary>
        TimedOut,
    }

    /// <summary>
    /// 협동 기술 "잡고 베기"(선우 11/13): 보스가 휘청이거나 빈틈일 때 왼팔이 정면으로 뻗어 그립으로 붙잡고 버티면,
    /// 그동안 오른팔이 벤 한 방에 큰 추가 피해가 들어간다. 혼자서는 못 한다(붙잡는 팔과 베는 팔이 다른 사람).
    /// 검을 잃은 맨손 구간에선 같은 규칙으로 "잡고 치기". 서버 시각으로만 진행. 순수 클래스.
    /// </summary>
    public sealed class GrabSlashRules
    {
        readonly GrabSlashSettings settings;
        double reachSince = double.NaN, heldSince = double.NaN, readyAt = double.NegativeInfinity;

        public GrabSlashRules(GrabSlashSettings settings = null)
        {
            this.settings = settings ?? new GrabSlashSettings();
        }

        public GrabSlashSettings Settings => settings;
        public bool Held => !double.IsNaN(heldSince);
        /// <summary>이 구간에서 베기까지 성공한 횟수(구간이 바뀌면 <see cref="NewPhase"/>로 0).</summary>
        public int Landed { get; private set; }
        /// <summary>보스가 이 수에 익숙해졌는지(빨리 뿌리친다).</summary>
        public bool Learned => Landed >= settings.learnAfter;
        /// <summary>지금 붙잡고 버틸 수 있는 시간(초).</summary>
        public float HoldSeconds => settings.maxHoldSeconds * (Learned ? settings.learnedHoldScale : 1f);
        public void NewPhase() => Landed = 0;
        /// <summary>지금 붙잡을 수 있는지(안내용): 보스가 잡을 수 있는 상태이고 쉬는 시간이 끝남.</summary>
        public bool Available(double now, bool grabbable) => !Held && grabbable && now >= readyAt;
        /// <summary>붙잡고 버틴 비율 0~1(1이면 곧 뿌리친다).</summary>
        public float HoldProgress(double now) => Held ? Mathf.Clamp01((float)((now - heldSince) / HoldSeconds)) : 0f;

        /// <param name="grabbable">보스가 휘청·빈틈·붙잡힘 상태인가.</param>
        /// <param name="leftHolding">왼팔이 정면으로 뻗고 그립을 쥐고 있는가.</param>
        public GrabStep Update(double now, bool grabbable, bool leftHolding)
        {
            if (Held)
            {
                if (!leftHolding || !grabbable)
                {
                    End(now);
                    return GrabStep.Broken;
                }
                if (now - heldSince < HoldSeconds) return GrabStep.None;
                End(now);
                return GrabStep.TimedOut;
            }
            if (!leftHolding || !grabbable || now < readyAt)
            {
                reachSince = double.NaN;
                return GrabStep.None;
            }
            if (double.IsNaN(reachSince)) reachSince = now;
            if (now - reachSince < settings.grabSeconds) return GrabStep.None;
            heldSince = now;
            reachSince = double.NaN;
            return GrabStep.Started;
        }

        /// <summary>오른팔 명중: 붙잡은 중이면 베기 성공(잡기는 끝난다).</summary>
        public bool TrySlash(double now)
        {
            if (!Held) return false;
            End(now);
            Landed++;
            return true;
        }

        public void Reset()
        {
            reachSince = heldSince = double.NaN;
            readyAt = double.NegativeInfinity;
            Landed = 0;
        }

        void End(double now)
        {
            heldSince = reachSince = double.NaN;
            readyAt = now + settings.cooldownSeconds;
        }
    }
}
