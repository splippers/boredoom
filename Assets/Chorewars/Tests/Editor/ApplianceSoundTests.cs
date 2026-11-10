using System;
using Chorewars.Weapon;
using NUnit.Framework;

namespace Chorewars.Tests
{
    public class ApplianceSoundTests
    {
        const int Rate = 16000;
        static float[] Noise(Random r, float amp, float lowpass)
        {
            var s = new float[ApplianceSoundProfile.FrameSize]; float prev = 0f;
            for (int i = 0; i < s.Length; i++) { prev = prev * lowpass + ((float)r.NextDouble() * 2f - 1f) * amp; s[i] = prev; }
            return s;
        }

        static ApplianceSoundProfile Learn(Random r)
        {
            var p = new ApplianceSoundProfile();
            for (int i = 0; i < 40; i++) p.AddQuiet(ApplianceSoundProfile.Features(Noise(r, 0.001f, 0.9f), 1024, Rate));
            for (int i = 0; i < 40; i++) p.AddRunning(ApplianceSoundProfile.Features(Noise(r, 0.2f, 0.5f), 1024, Rate));
            return p;
        }

        [Test] public void ToneLandsInTheRightBand()
        {
            var s = new float[1024];
            for (int i = 0; i < s.Length; i++) s[i] = (float)Math.Sin(2 * Math.PI * 1000 * i / Rate);
            var f = ApplianceSoundProfile.Features(s, 1024, Rate);
            int best = 0; for (int i = 1; i < f.Length; i++) if (f[i] > f[best]) best = i;
            Assert.AreEqual(3, best, "1 kHz is in the 800-1600 Hz band (index 3)");
        }

        [Test] public void LearnsAndTellsRunningFromQuiet()
        {
            var r = new Random(1); var p = Learn(r);
            Assert.IsTrue(p.TryFinish(out var problem), problem);
            Assert.Greater(p.Score(ApplianceSoundProfile.Features(Noise(r, 0.2f, 0.5f), 1024, Rate)), 0f);
            Assert.Less(p.Score(ApplianceSoundProfile.Features(Noise(r, 0.001f, 0.9f), 1024, Rate)), 0f);
        }

        [Test] public void TooLittleOrTooSimilarDataIsRejected()
        {
            var r = new Random(2); var p = new ApplianceSoundProfile();
            for (int i = 0; i < 5; i++) p.AddQuiet(ApplianceSoundProfile.Features(Noise(r, 0.01f, 0.9f), 1024, Rate));
            Assert.IsFalse(p.TryFinish(out _));
            p.Reset();
            for (int i = 0; i < 40; i++) { p.AddQuiet(ApplianceSoundProfile.Features(Noise(r, 0.01f, 0.9f), 1024, Rate)); p.AddRunning(ApplianceSoundProfile.Features(Noise(r, 0.01f, 0.9f), 1024, Rate)); }
            Assert.IsFalse(p.TryFinish(out var why)); StringAssert.Contains("alike", why);
        }

        [Test] public void ActivityDebouncesAndCountsRunningTime()
        {
            var a = new ApplianceActivity(5f); bool? last = null; a.Changed += v => last = v;
            for (int i = 0; i < 3; i++) a.Update(10f, 0.1f);
            Assert.IsFalse(a.Running, "0.3 s of noise is not yet running");
            for (int i = 0; i < 5; i++) a.Update(10f, 0.1f);
            Assert.IsTrue(a.Running); Assert.AreEqual(true, last);
            a.Update(-10f, 0.5f); a.Update(10f, 0.1f); a.Update(-10f, 0.5f);
            Assert.IsTrue(a.Running, "a brief dip must not stop it");
            for (int i = 0; i < 20; i++) a.Update(-10f, 0.1f);
            Assert.IsFalse(a.Running); Assert.AreEqual(false, last);
            Assert.Greater(a.ActiveSeconds, 1f);
        }
    }
}
