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

		public static float HorizontalRaw()
		{
			return Actions.Player.Move.ReadValue<Vector2>().x;
		}

		public static float VerticalRaw()
		{
			return Actions.Player.Move.ReadValue<Vector2>().y;
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
			return Actions.Player.Crouch.IsPressed();
		}
	}
}
