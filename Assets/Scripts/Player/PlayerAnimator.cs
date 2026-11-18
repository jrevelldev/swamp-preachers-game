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
		private static readonly int LedgeClimb = Animator.StringToHash("LedgeClimb");
		private static readonly int InputMagnitude = Animator.StringToHash("InputMagnitude");

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
			float targetSpeed = m_controller.isCrouching 
				? (m_controller.speed / Mathf.Max(1f, m_controller.crouchSpeedDivisor)) 
				: m_controller.speed;

			float absVelX = Mathf.Abs(m_rb.linearVelocity.x);
			if (absVelX < 0.05f) absVelX = 0f;

			// Idle & Running animation
			m_anim.SetFloat(Move, absVelX);
			
			// Normalized Input Speed (0 to 1) for variable animation speed (works for active player and companion)
			float inputMag = 0f;
			if (m_controller.isCrouching)
			{
				// In CrouchWalk, maintain full playback speed (1f) while moving so it never freezes in a single frame
				inputMag = absVelX > 0.05f ? 1f : 0f;
			}
			else if (Mathf.Abs(m_controller.moveInput) > 0.05f || absVelX > 0.05f)
			{
				inputMag = Mathf.Clamp01(Mathf.Max(Mathf.Abs(m_controller.moveInput), absVelX / Mathf.Max(0.1f, targetSpeed)));
			}
			m_anim.SetFloat(InputMagnitude, inputMag);

			// Jump state (handles transitions to falling/jumping)
			float verticalVelocity = m_rb.linearVelocity.y;
			m_anim.SetFloat(JumpState, verticalVelocity);

			// Jump animation
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
				float vInput = m_controller.GetVerticalRaw();
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
