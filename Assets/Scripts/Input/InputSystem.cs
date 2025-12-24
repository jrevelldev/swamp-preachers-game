using UnityEngine;

namespace SwampPreachers
{
	public class InputSystem : MonoBehaviour
	{
		// input string caching
		static readonly string HorizontalInput = "Horizontal";
		static readonly string JumpInput = "Jump";
		static readonly string DashInput = "Dash";

		public static float HorizontalRaw()
		{
			float input = 0f;
			if (UnityEngine.InputSystem.Keyboard.current != null)
			{
				if (UnityEngine.InputSystem.Keyboard.current.aKey.isPressed || UnityEngine.InputSystem.Keyboard.current.leftArrowKey.isPressed) input = -1f;
				if (UnityEngine.InputSystem.Keyboard.current.dKey.isPressed || UnityEngine.InputSystem.Keyboard.current.rightArrowKey.isPressed) input = 1f;
			}
			if (UnityEngine.InputSystem.Gamepad.current != null && input == 0f)
			{
				input = UnityEngine.InputSystem.Gamepad.current.leftStick.x.ReadValue();
			}
			return input;
		}

		public static bool Jump()
		{
			bool keyboardJump = UnityEngine.InputSystem.Keyboard.current != null && (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame || UnityEngine.InputSystem.Keyboard.current.wKey.wasPressedThisFrame || UnityEngine.InputSystem.Keyboard.current.upArrowKey.wasPressedThisFrame);
			bool gamepadJump = UnityEngine.InputSystem.Gamepad.current != null && UnityEngine.InputSystem.Gamepad.current.buttonSouth.wasPressedThisFrame;
			return keyboardJump || gamepadJump;
		}

		public static bool JumpHeld()
		{
			bool keyboardJump = UnityEngine.InputSystem.Keyboard.current != null && (UnityEngine.InputSystem.Keyboard.current.spaceKey.isPressed || UnityEngine.InputSystem.Keyboard.current.wKey.isPressed || UnityEngine.InputSystem.Keyboard.current.upArrowKey.isPressed);
			bool gamepadJump = UnityEngine.InputSystem.Gamepad.current != null && UnityEngine.InputSystem.Gamepad.current.buttonSouth.isPressed;
			return keyboardJump || gamepadJump;
		}

		public static bool Dash()
		{
			bool keyboardDash = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.leftShiftKey.wasPressedThisFrame;
			bool gamepadDash = UnityEngine.InputSystem.Gamepad.current != null && UnityEngine.InputSystem.Gamepad.current.buttonWest.wasPressedThisFrame;
			return keyboardDash || gamepadDash;
		}

		public static bool Attack()
		{
			bool mouseAttack = UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
			bool keyboardAttack = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame;
			bool gamepadAttack = UnityEngine.InputSystem.Gamepad.current != null && UnityEngine.InputSystem.Gamepad.current.buttonWest.wasPressedThisFrame;
			return mouseAttack || keyboardAttack || gamepadAttack;
		}
	}
}
