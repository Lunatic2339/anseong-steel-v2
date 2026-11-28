using System;
using System.Collections.Generic;
using Anseong.Skirmish;
using NUnit.Framework;
using UnityEngine;

namespace Anseong.Tests
{
    public class SkirmishSoundTests
    {
        [Test]
        public void EveryEventKindPlaysWithoutError()
        {
            var go = new GameObject("Sound test");
            try
            {
                var sound = go.AddComponent<SkirmishSound>();
                var clip = AudioClip.Create("beep", 4410, 1, 44100, false);
                sound.block = new SkirmishSound.Cue { clips = new[] { clip } };
                sound.heliRotor = new SkirmishSound.Cue { clips = new[] { clip } };
                var settings = SkirmishSettings.BossAct1At(Vector3.zero, 0f);
                var session = new SkirmishSession(settings);
                var events = new List<SkirmishEvent>();
                foreach (SkirmishEventKind kind in Enum.GetValues(typeof(SkirmishEventKind)))
                    events.Add(new SkirmishEvent { Kind = kind, Id = 1, From = Vector3.one, To = Vector3.up });
                Assert.DoesNotThrow(() => sound.Show(session.State, settings, events, SkirmishSeat.Mech, 0.02f));
                Assert.DoesNotThrow(() => sound.Show(session.State, settings, events, SkirmishSeat.Gunner, 0.02f));
                Assert.DoesNotThrow(() => sound.Show(session.State, settings, null, SkirmishSeat.Gunner, 0.02f));
                // 한 번 나는 소리 14개와 계속 나는 소리 6개가 만들어진다.
                Assert.AreEqual(20, go.GetComponentsInChildren<AudioSource>(true).Length);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
