using UnityEngine;

namespace Chorewars.HandsFree
{
    /// <summary>
    /// Something you can select by looking at it. Quest 3 has no eye tracking - that is
    /// Quest Pro - so "looking" means where the head points. That is coarse, so targets
    /// must be large and few: two or three on screen is the budget, not a menu.
    ///
    /// Nothing here draws anything yet; the dwell ring arrives with the outline work,
    /// which wants the same line-rendering machinery.
    /// </summary>
    public class GazeDwellTarget : MonoBehaviour
    {
        public string Label = "option";
        public ChorewarsIntent Intent = ChorewarsIntent.None;
        public string Argument;

        /// <summary>Half-angle of the cone that counts as looking at this, in degrees.</summary>
        public float AngularRadius = 7f;

        public bool Enabled = true;

        [System.NonSerialized] public bool Focused;
        [System.NonSerialized] public float Dwell;

        public void SetFocus(bool focused, float dwell01)
        {
            Focused = focused;
            Dwell = dwell01;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, 0.08f);

            var camera = Camera.main;
            if (camera == null) return;
            var forward = (transform.position - camera.transform.position).normalized;
            Gizmos.DrawLine(transform.position, transform.position + forward * 0.2f);
        }
    }
}
