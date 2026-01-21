using UnityEngine;

namespace SwampPreachers
{
	public class PlayerAnimator : MonoBehaviour
	{
		private Rigidbody2D m_rb;
		private PlayerController m_controller;
		private Animator m_anim;
		private static readonly int Move = Animator.StringToHash("Move");
		private static readonly int JumpState = Animator.StringToHash("JumpState");
		private static readonly int IsJumping = Animator.StringToHash("IsJumping");
		private static readonly int WallGrabbing = Animator.StringToHash("WallGrabbing");
		private static readonly int IsDashing = Animator.StringToHash("IsDashing");
		private static readonly int Melee = Animator.StringToHash("Melee");
		private static readonly int IsCrouching = Animator.StringToHash("IsCrouching");
		private static readonly int IsWallClimbing = Animator.StringToHash("IsWallClimbing");
		private static readonly int ClimbSpeed = Animator.StringToHash("ClimbSpeed");
		private static readonly int LedgeClimb = Animator.StringToHash("LedgeClimb"); // Trigger?

		[Header("Climbing Animation")]
		[SerializeField] private float climbAnimSpeedMultiplier = 0.5f;

		private void Start()
		{
			m_anim = GetComponentInChildren<Animator>();
			m_controller = GetComponent<PlayerController>();
			m_rb = GetComponent<Rigidbody2D>();
		}

		private void Update()
		{
			// Idle & Running animation
			m_anim.SetFloat(Move, Mathf.Abs(m_rb.linearVelocity.x));

			// Jump state (handles transitions to falling/jumping)
			float verticalVelocity = m_rb.linearVelocity.y;
			m_anim.SetFloat(JumpState, verticalVelocity);

			// Jump animation
			// DEBUG STATE
			// if (m_controller.isWallClimbing) Debug.Log("PlayerAnimator: Wall Climbing is TRUE");
			// else if (!m_controller.isGrounded) Debug.Log("PlayerAnimator: Air (Not Climbing)");

			if (!m_controller.isGrounded && !m_controller.actuallyWallGrabbing && !m_controller.isWallClimbing)
			{
				m_anim.SetBool(IsJumping, true);
			}
			else
			{
				m_anim.SetBool(IsJumping, false);
			}

			if(!m_controller.isGrounded && m_controller.actuallyWallGrabbing && !m_controller.isWallClimbing)
			{
				m_anim.SetBool(WallGrabbing, true);
			} else
			{
				m_anim.SetBool(WallGrabbing, false);
			}

			// dash animation
			m_anim.SetBool(IsDashing, m_controller.isDashing);

			// wall climbing animation
			// FIX: Only play wall climb if NOT ledge climbing
			bool isLedgeClimbing = m_controller.isLedgeClimbing;
			bool isClimbing = m_controller.isWallClimbing && !isLedgeClimbing;
			
			m_anim.SetBool(LedgeClimb, isLedgeClimbing);
			m_anim.SetBool(IsWallClimbing, isClimbing);
			
			if (isClimbing)
			{
				float vInput = GameInput.VerticalRaw();
				float direction = 0f;
				if (Mathf.Abs(vInput) > 0.01f)
				{
					direction = Mathf.Sign(vInput);
				}
				m_anim.SetFloat(ClimbSpeed, direction * climbAnimSpeedMultiplier);
			}

			// attack animation
			if(m_controller.attackTriggered)
			{
				m_anim.SetTrigger(Melee);
			}

			// crouch animation
			m_anim.SetBool(IsCrouching, m_controller.isCrouching);
		}
	}
}
