using UnityEngine;

namespace Chorewars.Orac
{
    /// <summary>
    /// ORAC's sound apparatus, synthesised at runtime so the build needs no audio assets.
    /// Hum and warble loop; sigh, brrp and the power cascades are one-shots.
    /// </summary>
    public class OracAudio : MonoBehaviour
    {
        const int Rate = 44100;
        const float Tau = 6.28318530718f;

        AudioSource _hum;
        AudioSource _warble;
        AudioSource _fx;

        AudioClip _humClip;
        AudioClip _warbleClip;
        AudioClip _sighClip;
        AudioClip _brrpClip;
        AudioClip _powerUpClip;
        AudioClip _powerDownClip;

        void Awake()
        {
            BuildClips();

            _hum = MakeSource(_humClip, true, 0f);
            _warble = MakeSource(_warbleClip, true, 0f);
            _fx = MakeSource(null, false, 1f);

            _hum.Play();
            _warble.Play();
        }

        AudioSource MakeSource(AudioClip clip, bool loop, float volume)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = loop;
            src.playOnAwake = false;
            src.volume = volume;
            src.spatialBlend = 1f;
            return src;
        }

        void BuildClips()
        {
            // 0.5 s at 120/240 Hz -> 60/120 whole cycles: seamless loop.
            _humClip = Tone("orac_hum", 0.5f, t =>
                0.05f * Mathf.Sin(Tau * 120f * t) +
                0.025f * Mathf.Sin(Tau * 240f * t));

            // 1.0 s at 620 Hz carrier / 8 Hz modulator -> whole cycles: seamless loop.
            _warbleClip = Tone("orac_warble", 1.0f, t =>
                0.09f * Mathf.Sin(Tau * 620f * t + 3.0f * Mathf.Sin(Tau * 8f * t)));

            _sighClip = Tone("orac_sigh", 1.35f, SighSample);
            _brrpClip = Tone("orac_brrp", 0.14f, BrrpSample);
            _powerUpClip = Tone("orac_powerup", 0.9f, PowerUpSample);
            _powerDownClip = Tone("orac_powerdown", 0.75f, PowerDownSample);
        }

        public void SetHumming(bool on)
        {
            if (_hum != null) _hum.volume = on ? 0.5f : 0f;
            if (!on && _hum != null && !_hum.isPlaying) _hum.Play();
        }

        public void SetProcessing(bool on)
        {
            if (_warble != null) _warble.volume = on ? 0.35f : 0f;
            if (!on && _warble != null && !_warble.isPlaying) _warble.Play();
        }

        public void PlayBrrp() { if (_fx != null && _brrpClip != null) _fx.PlayOneShot(_brrpClip, 0.9f); }
        public void PlaySigh() { if (_fx != null && _sighClip != null) _fx.PlayOneShot(_sighClip, 0.9f); }
        public void PlayPowerUp() { if (_fx != null && _powerUpClip != null) _fx.PlayOneShot(_powerUpClip, 0.9f); }
        public void PlayPowerDown() { if (_fx != null && _powerDownClip != null) _fx.PlayOneShot(_powerDownClip, 0.9f); }

        /// <summary>Kill everything instantly - the key has been yanked.</summary>
        public void StopAll()
        {
            if (_fx != null) _fx.Stop();
            SetProcessing(false);
        }

        static AudioClip Tone(string name, float seconds, System.Func<float, float> f)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds * Rate));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                data[i] = Mathf.Clamp(f(t), -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Noise(float x)
        {
            return Mathf.PerlinNoise(x, 0.37f) * 2f - 1f;
        }

        static float SighSample(float t)
        {
            float dur = 1.35f;
            float u = Mathf.Clamp01(t / dur);
            float pitch = Mathf.Lerp(520f, 190f, u) * (1f + 0.02f * Mathf.Sin(Tau * 5f * t));
            float body = 0.22f * Mathf.Sin(Tau * pitch * t);
            float breath = 0.05f * Noise(t * 3000f);
            float env = Mathf.Sin(Mathf.PI * u);
            return (body + breath) * env;
        }

        static float BrrpSample(float t)
        {
            float u = Mathf.Clamp01(t / 0.14f);
            float pitch = Mathf.Lerp(340f, 150f, u);
            float s = Mathf.Sign(Mathf.Sin(Tau * pitch * t));
            return 0.25f * s * Mathf.Pow(1f - u, 1.5f);
        }

        static float PowerUpSample(float t)
        {
            float u = Mathf.Clamp01(t / 0.9f);
            float pitch = Mathf.Lerp(180f, 1400f, u * u);
            return 0.22f * Mathf.Sin(Tau * pitch * t) * Mathf.SmoothStep(0f, 1f, u);
        }

        static float PowerDownSample(float t)
        {
            float u = Mathf.Clamp01(t / 0.75f);
            float pitch = Mathf.Lerp(1400f, 120f, u);
            return 0.22f * Mathf.Sin(Tau * pitch * t) * (1f - u);
        }
    }
}
