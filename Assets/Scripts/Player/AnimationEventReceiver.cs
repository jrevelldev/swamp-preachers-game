using UnityEngine;

namespace SwampPreachers
{
    public class AnimationEventReceiver : MonoBehaviour
    {
        private PlayerController m_playerController;

        private void Awake()
        {
            m_playerController = GetComponentInParent<PlayerController>();
            if (m_playerController == null)
            {
                Debug.LogWarning("AnimationEventReceiver: No PlayerController found in parent!");
            }
        }

        // Called by Animation Event
        public void TriggerAttackHit()
        {
            if (m_playerController != null)
            {
                m_playerController.TriggerAttackHit();
            }
        }
    }
}
