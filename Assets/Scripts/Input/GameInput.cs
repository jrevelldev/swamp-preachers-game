using UnityEngine;
using SwampPreachers.Input;

namespace SwampPreachers
{
	public static class GameInput
	{
		private static InputSystem_Actions _actions;

		public static InputSystem_Actions Actions
		{
			get
			{
				if (_actions == null)
				{
					_actions = new InputSystem_Actions();
					_actions.Enable();
				}
				return _actions;
			}
		}

		private const float DEADZONE = 0.25f;

		public static float HorizontalRaw()
		{
			float val = Actions.Player.Move.ReadValue<Vector2>().x;
			return Mathf.Abs(val) < DEADZONE ? 0f : val;
		}

		public static float VerticalRaw()
		{
			float val = Actions.Player.Move.ReadValue<Vector2>().y;
			return Mathf.Abs(val) < DEADZONE ? 0f : val;
		}

		public static bool Jump()
		{
			return Actions.Player.Jump.WasPressedThisFrame();
		}

		public static bool JumpHeld()
		{
			return Actions.Player.Jump.IsPressed();
		}

		public static bool Dash()
		{
			return Actions.Player.Dash.WasPressedThisFrame();
		}

		public static bool Attack()
		{
			return Actions.Player.Attack.WasPressedThisFrame();
		}

		public static bool Crouch()
		{
			bool actionCrouch = Actions.Player.Crouch.IsPressed();
			bool verticalDown = VerticalRaw() < -0.5f;
			return actionCrouch || verticalDown;
		}

		public static bool SwitchCharacter()
		{
			// Keyboard Tab or Q (C is strictly for Crouch)
			bool keyboardSwitch = false;
			if (UnityEngine.InputSystem.Keyboard.current != null)
			{
				keyboardSwitch = UnityEngine.InputSystem.Keyboard.current.tabKey.wasPressedThisFrame ||
				                 UnityEngine.InputSystem.Keyboard.current.qKey.wasPressedThisFrame;
			}

			// Gamepad buttonNorth (Y / Triangle) or rightShoulder or leftShoulder
			bool gamepadSwitch = false;
			if (UnityEngine.InputSystem.Gamepad.current != null)
			{
				gamepadSwitch = UnityEngine.InputSystem.Gamepad.current.buttonNorth.wasPressedThisFrame ||
				                UnityEngine.InputSystem.Gamepad.current.rightShoulder.wasPressedThisFrame ||
				                UnityEngine.InputSystem.Gamepad.current.leftShoulder.wasPressedThisFrame;
			}

			return keyboardSwitch || gamepadSwitch;
		}

		public static bool ToggleFollow()
		{
			// Keyboard F
			bool keyboardFollow = false;
			if (UnityEngine.InputSystem.Keyboard.current != null)
			{
				keyboardFollow = UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame;
			}

			// Gamepad D-pad Down or Left Stick click (L3)
			bool gamepadFollow = false;
			if (UnityEngine.InputSystem.Gamepad.current != null)
			{
				gamepadFollow = UnityEngine.InputSystem.Gamepad.current.dpad.down.wasPressedThisFrame ||
				                UnityEngine.InputSystem.Gamepad.current.leftStickButton.wasPressedThisFrame;
			}

			return keyboardFollow || gamepadFollow;
		}
	}
}
