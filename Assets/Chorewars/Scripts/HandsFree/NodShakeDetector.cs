using UnityEngine;

namespace Chorewars.HandsFree
{
    public class NodShakeDetector : MonoBehaviour
    {
        public bool Enabled = true;
        public NodShakeLogic Logic = new NodShakeLogic();

        /// <summary>Raised with Nod ("yes") or Shake ("no"). Never with None.</summary>
        public event System.Action<HeadGesture> Gesture;

        Transform _head;

        public HeadGesture LastGesture { get; private set; }

        void Update()
        {
            if (!Enabled) return;

            if (_head == null)
            {
                var camera = Camera.main;
                if (camera == null) return;
                _head = camera.transform;
            }

            float pitch = Mathf.DeltaAngle(0f, _head.localEulerAngles.x);
            float yaw = Mathf.DeltaAngle(0f, _head.localEulerAngles.y);

            var gesture = Logic.Feed(pitch, yaw, Time.unscaledDeltaTime, Time.unscaledTime);
            if (gesture == HeadGesture.None) return;

            LastGesture = gesture;
            if (Gesture != null) Gesture(gesture);
        }
    }
}
