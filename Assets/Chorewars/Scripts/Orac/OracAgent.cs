using UnityEngine;

namespace Chorewars.Orac
{
    /// <summary>
    /// The brain seam. Whichever mind is behind ORAC - the OpenCode bridge to the dev
    /// team, or an in-headset model - implements this. ORAC is the body either way.
    /// </summary>
    public interface IOracBrain
    {
        void Ask(string question, System.Action<string> onReply);
    }

    /// <summary>
    /// ORAC himself: the key state machine, the moods, and the seam to a brain.
    /// Key in = alive; key out = instantly inert, mid-sentence.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class OracAgent : MonoBehaviour
    {
        public static OracAgent Instance { get; private set; }

        public OracMood Mood { get; private set; }
        public bool KeyInserted { get; private set; }
        public bool HasBrain { get { return _brain != null; } }
        public string LastLine { get { return _lastLine; } }

        /// <summary>Fired whenever ORAC says something. Text is the raw line.</summary>
        public event System.Action<string> Spoke;

        OracVisual _visual;
        OracAudio _audio;
        IOracBrain _brain;
        int _salt;
        string _lastLine = string.Empty;

        void Awake()
        {
            if (Instance == null) Instance = this;
            _salt = Random.Range(int.MinValue, int.MaxValue);

            _visual = GetComponentInChildren<OracVisual>();
            if (_visual == null) _visual = gameObject.AddComponent<OracVisual>();
            _visual.Build();

            _audio = GetComponentInChildren<OracAudio>();
            if (_audio == null) _audio = gameObject.AddComponent<OracAudio>();

            ApplyMood(OracMood.Dormant);
        }

        public void SetBrain(IOracBrain brain) { _brain = brain; }

        public void InsertKey()
        {
            if (KeyInserted) return;
            KeyInserted = true;
            if (_visual != null) _visual.SetKeyInserted(true);
            if (_audio != null)
            {
                _audio.SetHumming(true);
                _audio.PlayPowerUp();
            }
            ApplyMood(OracMood.Idle);
            Say(OracLineContext.PowerUp);
        }

        public void WithdrawKey()
        {
            if (!KeyInserted) return;
            KeyInserted = false;
            if (_audio != null)
            {
                _audio.StopAll();          // cut him off mid-sentence, as canon demands
                _audio.PlayPowerDown();
                _audio.SetHumming(false);
            }
            if (_visual != null) _visual.SetKeyInserted(false);
            ApplyMood(OracMood.Dormant);
        }

        public void Ask(string question)
        {
            if (!KeyInserted)
            {
                Say(OracLineContext.Shutdown);
                return;
            }

            ApplyMood(OracMood.Processing);
            if (_audio != null) _audio.SetProcessing(true);

            if (_brain != null)
            {
                _brain.Ask(question, reply =>
                {
                    if (_audio != null) _audio.SetProcessing(false);
                    ApplyMood(OracMood.Answering);
                    Speak(reply);
                });
            }
            else
            {
                // No brain attached yet: refuse, in character, so the body is demonstrable.
                if (_audio != null) _audio.SetProcessing(false);
                ApplyMood(OracMood.Sulking);
                Say(OracLineContext.Refused);
            }
        }

        public void Say(OracLineContext context)
        {
            if (context == OracLineContext.Sigh && _audio != null) _audio.PlaySigh();
            Speak(OracPersona.Pick(context, _salt++));
        }

        public void Speak(string text)
        {
            _lastLine = text;
            if (_audio != null) _audio.PlayBrrp();
            if (_visual != null && _visual.Sequencer != null) _visual.Sequencer.Pulse();
            if (Mood != OracMood.Dormant) ApplyMood(OracMood.Answering);
            if (Spoke != null) Spoke(text);
        }

        public void SetMood(OracMood mood)
        {
            if (Mood == OracMood.Dormant && mood == OracMood.Dormant) return;
            ApplyMood(mood);
        }

        void ApplyMood(OracMood mood)
        {
            Mood = mood;
            if (_visual != null && _visual.Sequencer != null) _visual.Sequencer.SetMood(mood);
        }

        public static OracAgent EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("ORAC");
            return go.AddComponent<OracAgent>();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("BoreDOOM/ORAC/Spawn ORAC")]
        static void MenuSpawn()
        {
            var orac = EnsureExists();
            orac.transform.position = new Vector3(0f, 1.1f, 0.6f);
            orac.InsertKey();
            UnityEditor.SceneView.RepaintAll();
        }

        [UnityEditor.MenuItem("BoreDOOM/ORAC/Toggle key")]
        static void MenuToggleKey()
        {
            var orac = Instance;
            if (orac == null) { MenuSpawn(); return; }
            if (orac.KeyInserted) orac.WithdrawKey(); else orac.InsertKey();
        }

        [UnityEditor.MenuItem("BoreDOOM/ORAC/Ask something impertinent")]
        static void MenuAsk()
        {
            var orac = Instance;
            if (orac == null) return;
            orac.Ask("Explain yourself.");
        }
#endif
    }
}
