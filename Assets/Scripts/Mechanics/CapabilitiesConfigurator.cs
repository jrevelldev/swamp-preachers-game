using UnityEngine;
using UnityEngine.UI;

namespace SwampPreachers
{
    public class CapabilitiesConfigurator : MonoBehaviour
    {
        public enum CapabilityMode
        {
            Ignore,
            Enable,
            Disable,
            Toggle
        }

        [Header("Configuration")]
        public CapabilityMode jump = CapabilityMode.Ignore;
        public CapabilityMode doubleJump = CapabilityMode.Ignore;
        public CapabilityMode dash = CapabilityMode.Ignore;
        public CapabilityMode crouch = CapabilityMode.Ignore;
        public CapabilityMode attack = CapabilityMode.Ignore;
        public CapabilityMode airAttack = CapabilityMode.Ignore;

        [Header("Area Settings")]
        [Tooltip("If true, capabilities will be reverted to their state at entry when the player exits the trigger. Only applies to capabilities not set to 'Ignore'.")]
        public bool revertOnExit = false;

        [Header("Text Popup")]
        [Tooltip("Assign a UI Text object to show a message.")]
        public Text popText;
        [TextArea] public string message;



        private struct SavedCapabilities
        {
            public bool jump;
            public bool doubleJump;
            public bool dash;
            public bool crouch;
            public bool attack;
            public bool airAttack;
        }
        private SavedCapabilities m_savedCaps;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerController player = other.GetComponent<PlayerController>();
                if (player != null)
                {
                    if (revertOnExit)
                    {
                        m_savedCaps = new SavedCapabilities
                        {
                            jump = player.enableJump,
                            doubleJump = player.enableDoubleJump,
                            dash = player.enableDash,
                            crouch = player.enableCrouch,
                            attack = player.enableAttack,
                            airAttack = player.enableAirAttack
                        };
                    }

                    ApplyCapability(ref player.enableJump, jump);
                    ApplyCapability(ref player.enableDoubleJump, doubleJump);
                    ApplyCapability(ref player.enableDash, dash);
                    ApplyCapability(ref player.enableCrouch, crouch);
                    ApplyCapability(ref player.enableAttack, attack);
                    ApplyCapability(ref player.enableAirAttack, airAttack);
                }

                if (popText != null)
                {
                    popText.text = message;
                    popText.gameObject.SetActive(true);
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                if (revertOnExit)
                {
                    PlayerController player = other.GetComponent<PlayerController>();
                    if (player != null)
                    {
                        if (jump != CapabilityMode.Ignore) player.enableJump = m_savedCaps.jump;
                        if (doubleJump != CapabilityMode.Ignore) player.enableDoubleJump = m_savedCaps.doubleJump;
                        if (dash != CapabilityMode.Ignore) player.enableDash = m_savedCaps.dash;
                        if (crouch != CapabilityMode.Ignore) player.enableCrouch = m_savedCaps.crouch;
                        if (attack != CapabilityMode.Ignore) player.enableAttack = m_savedCaps.attack;
                        if (airAttack != CapabilityMode.Ignore) player.enableAirAttack = m_savedCaps.airAttack;
                    }
                }

                if (popText != null)
                {
                    popText.gameObject.SetActive(false);
                }
            }
        }

        private void ApplyCapability(ref bool capability, CapabilityMode mode)
        {
            switch (mode)
            {
                case CapabilityMode.Enable:
                    capability = true;
                    break;
                case CapabilityMode.Disable:
                    capability = false;
                    break;
                case CapabilityMode.Toggle:
                    capability = !capability;
                    break;
                case CapabilityMode.Ignore:
                default:
                    break;
            }
        }

        private void OnDrawGizmos()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.4f); // Semi-transparent green
                Gizmos.DrawCube(col.bounds.center, col.bounds.size);
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            }
        }
    }
}
