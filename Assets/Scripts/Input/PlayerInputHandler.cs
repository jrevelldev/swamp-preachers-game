using UnityEngine;
using UnityEngine.InputSystem;

namespace SwampPreachers
{
	/// <summary>
	/// Handles input for a specific player from a specific gamepad.
	/// Used for local co-op to separate Player 1 and Player 2 controls.
	/// </summary>
	public class PlayerInputHandler : MonoBehaviour
	{
		[Header("Gamepad Configuration")]
		[SerializeField] 
		[Tooltip("Gamepad index (0 for Player 1, 1 for Player 2)")]
		private int gamepadIndex = 0;

		private const float DEADZONE = 0.25f;
		private Gamepad activeGamepad;

		private void Start()
		{
			UpdateGamepad();
		}

		private void Update()
		{
			// Dynamically update gamepad reference in case controllers are connected/disconnected
			if (activeGamepad == null || !activeGamepad.enabled)
			{
				UpdateGamepad();
			}
		}

		private void UpdateGamepad()
		{
			if (Gamepad.all.Count > gamepadIndex)
			{
				activeGamepad = Gamepad.all[gamepadIndex];
			}
			else
			{
				activeGamepad = null;
				if (gamepadIndex == 0 && Gamepad.all.Count > 0)
				{
					// Fallback: If Player 1 and any gamepad exists, use the first one
					activeGamepad = Gamepad.all[0];
				}
			}
		}

		public float HorizontalRaw()
		{
			if (activeGamepad == null) return 0f;
			
			float val = activeGamepad.leftStick.x.ReadValue();
			return Mathf.Abs(val) < DEADZONE ? 0f : val;
		}

		public float VerticalRaw()
		{
			if (activeGamepad == null) return 0f;
			
			float val = activeGamepad.leftStick.y.ReadValue();
			return Mathf.Abs(val) < DEADZONE ? 0f : val;
		}

		public bool Jump()
		{
			if (activeGamepad == null) return false;
			return activeGamepad.buttonSouth.wasPressedThisFrame;
		}

		public bool JumpHeld()
		{
			if (activeGamepad == null) return false;
			return activeGamepad.buttonSouth.isPressed;
		}

		public bool Dash()
		{
			if (activeGamepad == null) return false;
			return activeGamepad.rightShoulder.wasPressedThisFrame;
		}

		public bool Attack()
		{
			if (activeGamepad == null) return false;
			return activeGamepad.buttonWest.wasPressedThisFrame;
		}

		public bool Crouch()
		{
			if (activeGamepad == null) return false;
			return activeGamepad.buttonEast.isPressed;
		}

		// Public getter for debugging
		public int GamepadIndex => gamepadIndex;
		public bool IsGamepadConnected => activeGamepad != null && activeGamepad.enabled;
	}
}
