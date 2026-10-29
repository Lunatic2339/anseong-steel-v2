using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Anseong.Core
{
    /// <summary>
    /// 인게임 조절 메뉴에 항목을 내놓는 컴포넌트(선우 10/25: 헤드셋을 쓴 채 값 조절).
    /// 조절값이 있는 컴포넌트는 [SerializeField] + [Tooltip]에 더해 이것을 구현해 같은 값을 메뉴에도 등록한다.
    /// </summary>
    public interface ITunable
    {
        void AddTuning(TuningList list);
    }

    /// <summary>메뉴 항목 하나. 같은 키로 여러 번 등록하면 한 항목이 모두를 함께 바꾼다(예: 왼팔·오른팔).</summary>
    public sealed class TuningItem
    {
        public string Key, Group, Label;
        public float Min, Max, Step, Default;
        public int Decimals;
        /// <summary>null이 아니면 선택지(값 = 순번). 켜고 끄기는 { "끔", "켬" }.</summary>
        public string[] Choices;
        /// <summary>검증 체크 항목(값 = 아직/OK/문제). 사람이 헤드셋 안에서 확인한 결과를 적는다.</summary>
        public bool IsCheck;
        public Func<float> Get;
        public Action<float> Set;

        public float Value => Get != null ? Get() : 0f;
        public bool IsChoice => Choices != null && Choices.Length > 0;

        public string ValueText()
        {
            var v = Value;
            if (IsChoice) return Choices[Mathf.Clamp(Mathf.RoundToInt(v), 0, Choices.Length - 1)];
            return v.ToString("F" + Decimals, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// 인게임 조절 메뉴의 항목 목록과 고르기·바꾸기·저장 문자열(순수). 화면과 입력은 UI.TuningMenu가 한다.
    /// 저장 형식은 "키=값" 한 줄씩. 시작값(등록할 때 값 = 씬·프리팹 값)과 다른 값만 쓴다.
    /// </summary>
    public sealed class TuningList
    {
        readonly List<TuningItem> items = new List<TuningItem>();
        readonly Dictionary<string, TuningItem> byKey = new Dictionary<string, TuningItem>();
        readonly Dictionary<string, string> hints = new Dictionary<string, string>();
        readonly Dictionary<string, int> checks = new Dictionary<string, int>();

        /// <summary>검증 체크 선택지. 0 = 아직(시작값).</summary>
        public static readonly string[] CheckChoices = { "아직", "OK", "문제" };
        public const int CheckOk = 1, CheckProblem = 2;

        public IReadOnlyList<TuningItem> Items => items;
        public int Selected { get; private set; }
        public TuningItem Current => items.Count > 0 ? items[Selected] : null;

        public TuningItem AddFloat(string group, string key, string label, Func<float> get, Action<float> set,
            float min, float max, float step, int decimals = 2)
        {
            return Add(group, key, label, get, set, min, max, step, decimals, null);
        }

        public TuningItem AddToggle(string group, string key, string label, Func<bool> get, Action<bool> set)
        {
            return Add(group, key, label, () => get() ? 1f : 0f, v => set(v >= 0.5f), 0f, 1f, 1f, 0, new[] { "끔", "켬" });
        }

        public TuningItem AddChoice(string group, string key, string label, Func<int> get, Action<int> set, string[] names)
        {
            return Add(group, key, label, () => get(), v => set(Mathf.RoundToInt(v)), 0f, names.Length - 1, 1f, 0, names);
        }

        /// <summary>묶음의 검증 안내 한 줄(무엇을 하고 무엇을 볼지). 메뉴가 그 묶음을 고르면 위에 띄운다. 먼저 등록한 것을 쓴다.</summary>
        public void Describe(string group, string hint)
        {
            if (!string.IsNullOrEmpty(hint) && !hints.ContainsKey(group)) hints[group] = hint;
        }

        public string Hint(string group) => group != null && hints.TryGetValue(group, out var hint) ? hint : null;

        /// <summary>헤드셋에서 확인할 것 하나(아직/OK/문제). 값은 목록이 들고 있고 조절값처럼 저장 파일에 남는다.</summary>
        public TuningItem AddCheck(string group, string key, string label)
        {
            var fullKey = "check." + key;
            if (!checks.ContainsKey(fullKey)) checks[fullKey] = 0;
            var item = AddChoice(group, fullKey, "[확인] " + label, () => checks[fullKey], v => checks[fullKey] = v, CheckChoices);
            item.IsCheck = true;
            return item;
        }

        /// <summary>검증 체크 개수: 전체, OK, 문제.</summary>
        public void CountChecks(out int total, out int ok, out int problem)
        {
            total = ok = problem = 0;
            foreach (var item in items)
            {
                if (!item.IsCheck) continue;
                total++;
                var v = Mathf.RoundToInt(item.Value);
                if (v == CheckOk) ok++;
                else if (v == CheckProblem) problem++;
            }
        }

        TuningItem Add(string group, string key, string label, Func<float> get, Action<float> set,
            float min, float max, float step, int decimals, string[] choices)
        {
            if (byKey.TryGetValue(key, out var existing))
            {
                // 같은 키 = 같은 값을 쓰는 다른 컴포넌트(왼팔·오른팔): 바꿀 때 모두 바꾼다.
                existing.Set += set;
                return existing;
            }
            var item = new TuningItem
            {
                Key = key, Group = group, Label = label, Get = get, Set = set,
                Min = min, Max = max, Step = step, Decimals = decimals, Choices = choices, Default = get(),
            };
            items.Add(item);
            byKey[key] = item;
            return item;
        }

        /// <summary>묶음 순서대로 정렬(같은 묶음 안은 등록 순서). 목록에 없는 묶음은 뒤로.</summary>
        public void SortByGroup(IList<string> order)
        {
            Func<TuningItem, int> Rank = item =>
            {
                var i = order != null ? order.IndexOf(item.Group) : -1;
                return i < 0 ? int.MaxValue : i;
            };
            var sorted = new List<TuningItem>(items);
            var index = new Dictionary<TuningItem, int>();
            for (var i = 0; i < sorted.Count; i++) index[sorted[i]] = i;
            sorted.Sort((a, b) =>
            {
                var r = Rank(a).CompareTo(Rank(b));
                return r != 0 ? r : index[a].CompareTo(index[b]);
            });
            items.Clear();
            items.AddRange(sorted);
            Selected = 0;
        }

        public void Clear()
        {
            items.Clear();
            byKey.Clear();
            hints.Clear();
            checks.Clear();
            Selected = 0;
        }

        /// <summary>위아래로 고르기. 끝에서 멈춘다(반대쪽으로 넘어가면 헷갈림, 선우 10/25).</summary>
        public void Move(int delta)
        {
            if (items.Count == 0) return;
            Selected = Mathf.Clamp(Selected + delta, 0, items.Count - 1);
        }

        /// <summary>지금 항목을 steps 칸 바꾼다. 선택지·수치 모두 끝에서 멈추고(돌아가지 않음), 수치는 칸에 맞춘다. 바뀌었으면 true.</summary>
        public bool Adjust(int steps)
        {
            var item = Current;
            if (item == null || steps == 0) return false;
            var before = item.Value;
            float next;
            if (item.IsChoice)
            {
                var count = item.Choices.Length;
                next = Mathf.Clamp(Mathf.RoundToInt(before) + steps, 0, count - 1);
            }
            else
            {
                next = Mathf.Clamp(before + steps * item.Step, item.Min, item.Max);
                next = Snap(next, item);
            }
            if (Mathf.Approximately(next, before)) return false;
            item.Set(next);
            return true;
        }

        /// <summary>지금 항목을 시작값으로. 바뀌었으면 true.</summary>
        public bool ResetCurrent()
        {
            var item = Current;
            if (item == null || Mathf.Approximately(item.Value, item.Default)) return false;
            item.Set(item.Default);
            return true;
        }

        static float Snap(float value, TuningItem item)
        {
            if (item.Step <= 0f) return value;
            // 기본값이 칸에 안 맞아도(예: 0.065) 기본값 기준으로 칸을 센다.
            var steps = Mathf.Round((value - item.Default) / item.Step);
            return (float)Math.Round(Mathf.Clamp(item.Default + steps * item.Step, item.Min, item.Max), 6);
        }

        /// <summary>시작값과 다른 항목만 "키=값" 줄로.</summary>
        public string Serialize()
        {
            var text = new StringBuilder();
            foreach (var item in items)
            {
                if (Mathf.Approximately(item.Value, item.Default)) continue;
                text.Append(item.Key).Append('=').Append(item.Value.ToString("R", CultureInfo.InvariantCulture))
                    .Append("    # ").Append(item.Group).Append(" / ").Append(item.Label)
                    .Append(" (시작 ").Append(item.Default.ToString("R", CultureInfo.InvariantCulture)).Append(")\n");
            }
            return text.ToString();
        }

        /// <summary>저장한 줄을 적용한다. 모르는 키·틀린 줄은 건너뛴다. 적용한 개수.</summary>
        public int Apply(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var applied = 0;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw;
                var comment = line.IndexOf('#');
                if (comment >= 0) line = line.Substring(0, comment);
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line.Substring(0, eq).Trim();
                if (!byKey.TryGetValue(key, out var item)) continue;
                if (!float.TryParse(line.Substring(eq + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) continue;
                item.Set(item.IsChoice ? Mathf.Clamp(Mathf.Round(value), item.Min, item.Max) : Mathf.Clamp(value, item.Min, item.Max));
                applied++;
            }
            return applied;
        }
    }
}
