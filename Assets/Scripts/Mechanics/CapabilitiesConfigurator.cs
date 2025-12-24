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

        [Header("Text Popup")]
        [Tooltip("Assign a UI Text object to show a message.")]
        public Text popText;
        [TextArea] public string message;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerController player = other.GetComponent<PlayerController>();
                if (player != null)
                {
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
