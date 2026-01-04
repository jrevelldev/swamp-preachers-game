using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwampPreachers
{
	public class PlayerController : MonoBehaviour
	{
		[Header("Capabilities")]
		[SerializeField] public bool enableJump = true;
		[SerializeField] public bool enableDoubleJump = false;
		[SerializeField] public bool enableDash = true;
		[SerializeField] public bool enableCrouch = true;
		[SerializeField] public bool enableAttack = true;
		[SerializeField] public bool enableAirAttack = false;

		[Header("Health")]
		[SerializeField] private int maxHealth = 3;
		[SerializeField] private float deathDelay = 2f;
		private int currentHealth;
		private bool m_hasTriggeredDeath = false;

		[Header("Health UI")]
		[SerializeField] private bool showHealthBar = true;
		[SerializeField] private Vector2 healthBarSize = new Vector2(1f, 0.15f);
		[SerializeField] private Vector2 healthBarOffset = new Vector2(0f, 0.8f);
		[SerializeField] private Vector2 crouchHealthBarOffset = new Vector2(0f, 0.5f); // Lower position for crouching
		[SerializeField] private float healthBarSmoothSpeed = 10f; // Control the transition speed
		[SerializeField] private float crouchUiDelay = 0f; // Delay before UI moves down (to match animation)
		
		private float m_crouchDelayTimer;
		private bool m_wasCrouching;

		private Transform m_healthBarRoot;
		private Transform m_healthBarFill;

		[Header("Movement")]
		[SerializeField] private float speed;
		[SerializeField] private float crouchSpeedDivisor = 2f;

		[Header("Jumping")]
		[SerializeField] private float jumpForce;
		[SerializeField] private float fallMultiplier;
		[SerializeField] private float lowJumpMultiplier = 2f;
		[SerializeField] private float jumpBufferTime = 0.1f;
		[SerializeField] private float coyoteTime = 0.25f;
		[SerializeField] private int extraJumpCount = 1;
		[SerializeField] private GameObject jumpEffect;

		[Header("Dashing")]
		[SerializeField] private float dashSpeed = 30f;
		[Tooltip("Amount of time (in seconds) the player will be in the dashing speed")]
		[SerializeField] private float startDashTime = 0.1f;
		[Tooltip("Time (in seconds) between dashes")]
		[SerializeField] private float dashCooldown = 0.2f;
		[SerializeField] private GameObject dashEffect;

		[Header("Ground Detection")]
		[SerializeField] private Transform groundCheck;
		[SerializeField] private float groundCheckRadius;
		[SerializeField] private LayerMask whatIsGround;

		[Header("Combat")]
		[SerializeField] private float attackSpeedDivisor = 2f;
		[SerializeField] private float attackSlowdownDuration = 0.4f;
		[SerializeField] private int attackDamage = 1;
		[SerializeField] private float attackRange = 0.5f;
		[SerializeField] private float attackKnockback = 5f;
		[SerializeField] private float bounceForce = 15f;
		[SerializeField] private Transform attackPoint;
		[SerializeField] private LayerMask enemyLayers;

		[Header("Combat Reaction")]
		[SerializeField] private Vector2 knockbackForce = new Vector2(5f, 10f);
		[SerializeField] private float hurtDuration = 0.5f;
		
		private float m_hurtTimer;
		[HideInInspector] public bool isHurt = false;

		// Access needed for handling animation in Player script and other uses
		[HideInInspector] public bool isGrounded;
		[HideInInspector] public float moveInput;
		[HideInInspector] public bool canMove = true;
		[HideInInspector] public bool isDashing = false;
		[HideInInspector] public bool isAttacking = false;
		[HideInInspector] public bool actuallyWallGrabbing = false;
		// controls whether this instance is currently playable or not
		[HideInInspector] public bool isCurrentlyPlayable = false;

		[Header("Wall Movement")]
		[SerializeField] private LayerMask whatIsWall;
		[Tooltip("Right offset of the wall detection sphere")]
		public Vector2 grabRightOffset = new Vector2(0.16f, 0.2f);
		public Vector2 grabLeftOffset = new Vector2(-0.16f, 0.2f);
		public float grabCheckRadius = 0.24f;
		public float slideSpeed = 2.5f;
		public Vector2 wallJumpForce = new Vector2(10.5f, 18f);

		public Vector2 wallClimbForce = new Vector2(4f, 14f);
		


		// [Header("Climbing")] -> Movable to Animator script if needed, or keep for physics
		[SerializeField] public float climbSpeed = 3f;
		[SerializeField] private LayerMask whatIsClimbable;
		[SerializeField] private Vector2 ledgeCheckOffset = new Vector2(0.16f, 0.4f); // Check above Grab Offset
		[SerializeField] private Vector2 ledgeTeleportOffset = new Vector2(1.0f, 1.5f); // X (Forward), Y (Up)
		// Animation params moved to PlayerAnimator
		// [SerializeField] private string climbingStateParam = "IsWallClimbing";
		// [SerializeField] private string climbingSpeedParam = "ClimbSpeed";
		// [SerializeField] private float climbAnimSpeedMultiplier = 0.5f; // Slower animation

		[Header("Camera Look")]
		[SerializeField] private float lookDelay = 0.5f;
		[SerializeField] private float lookDistance = 3f;
		private float m_lookTimer;
		private CameraFollow m_cam;

		private Rigidbody2D m_rb;
		private float m_defaultGravity;
		private ParticleSystem m_dustParticle;
		private bool m_facingRight = true;
		private float m_groundedRemember = 0f;
		private int m_extraJumps;
		private float m_extraJumpForce;
		private float m_dashTime;
		private bool m_hasDashedInAir = false;
		private bool m_onWall = false;
		private bool m_onRightWall = false;
		private bool m_onLeftWall = false;
		private bool m_wallGrabbing = false;
		// Make public/property so PlayerAnimator can read it
		// Changed: Exclude Ledge Climbing from Wall Climbing state so they are distinct
		public bool isWallClimbing => m_isWallClimbing; 
		public bool isLedgeClimbing => m_isLedgeClimbing;
		private bool m_isWallClimbing = false;
		private bool m_isLedgeClimbing = false;
		private readonly float m_wallStickTime = 0.25f;
		private float m_wallStick = 0f;
		private bool m_wallJumping = false;
		private float m_dashCooldown;
		private float m_attackSlowdownTimer;
		private float m_jumpBufferCounter;
		private BoxCollider2D m_collider;
		private Vector2 m_originalColliderSize;
		private Vector2 m_originalColliderOffset;
		[HideInInspector] public bool isCrouching = false;
		private SpriteRenderer m_spriteRenderer;
		private Animator m_animator;
		
		// 0 -> none, 1 -> right, -1 -> left
		private int m_onWallSide = 0;
		private int m_playerSide = 1;


		void Start()
		{
			// create pools for particles
			PoolManager.instance.CreatePool(dashEffect, 2);
			PoolManager.instance.CreatePool(jumpEffect, 2);

			// if it's the player, make this instance currently playable
			if (transform.CompareTag("Player"))
				isCurrentlyPlayable = true;

			m_extraJumps = extraJumpCount;
			m_dashTime = startDashTime;
			m_dashCooldown = dashCooldown;
			m_extraJumpForce = jumpForce * 0.7f;

			m_rb = GetComponent<Rigidbody2D>();
			m_defaultGravity = m_rb.gravityScale;
			m_collider = GetComponent<BoxCollider2D>();
			m_originalColliderSize = m_collider.size;
			m_originalColliderOffset = m_collider.offset;
			m_dustParticle = GetComponentInChildren<ParticleSystem>();
			m_spriteRenderer = GetComponentInChildren<SpriteRenderer>();
			m_animator = GetComponentInChildren<Animator>();
			m_cam = FindFirstObjectByType<CameraFollow>();
			currentHealth = maxHealth;
			
			// Auto-setup shader if possible
			// Assuming the user didn't assign a material manually, we might want to ensure we can flash.
			// But for now, we rely on the shader property being present or we swap shader.
			// Ideally, we just find the shader.
			if (m_spriteRenderer != null)
			{
				m_spriteRenderer.material.shader = Shader.Find("SwampPreachers/SpriteFlash");
			}

			// Debug Warnings
			if (whatIsWall.value == 0) Debug.LogWarning("PlayerController: 'What Is Wall' LayerMask is empty!");
			if (whatIsClimbable.value == 0) Debug.LogWarning("PlayerController: 'What Is Climbable' LayerMask is empty!");
			
			if (showHealthBar)
				CreateHealthBar();
		}

		private void FixedUpdate()
		{
			// If Ledge Climbing, physics are controlled by coroutine. Do NOT run update.
			if (m_isLedgeClimbing) return;

			// check if grounded
			isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, whatIsGround);
			var position = transform.position;
			// check if on wall (Include Climbable as Wall for attachment)
			LayerMask combinedWallMask = whatIsWall | whatIsClimbable;
			m_onWall = Physics2D.OverlapCircle((Vector2)position + grabRightOffset, grabCheckRadius, combinedWallMask)
			          || Physics2D.OverlapCircle((Vector2)position + grabLeftOffset, grabCheckRadius, combinedWallMask);
			m_onRightWall = Physics2D.OverlapCircle((Vector2)position + grabRightOffset, grabCheckRadius, combinedWallMask);
			m_onLeftWall = Physics2D.OverlapCircle((Vector2)position + grabLeftOffset, grabCheckRadius, combinedWallMask);
			
			// Reset gravity if not climbing
			if(!m_isWallClimbing && !m_isLedgeClimbing) 
				m_rb.gravityScale = m_defaultGravity;

			// calculate player and wall sides as integers
			CalculateSides();

			// Update Animator logic moved to PlayerAnimator.cs
			// if (m_animator != null)
			// 	m_animator.SetBool(climbingStateParam, m_isWallClimbing);

			if((m_wallGrabbing || isGrounded) && m_wallJumping)
			{
				m_wallJumping = false;
			}
			if((m_wallGrabbing || isGrounded) && m_wallJumping)
			{
				m_wallJumping = false;
			}
			if (!isCurrentlyPlayable)
			{
				// Safety enforcement for Death state:
				// Ensure gravity is ON so we fall (fix 'floating' bug)
				if (m_rb.gravityScale <= 0.1f) m_rb.gravityScale = 3f; // Hardcode to 3 to ensure fall
				
				// Clamp upward velocity to prevent floating away if impulse was high
				if (m_rb.linearVelocity.y > 0f) 
				{
					m_rb.linearVelocity = new Vector2(m_rb.linearVelocity.x, m_rb.linearVelocity.y * 0.9f); // Dampen upward
				}
				
				// Ensure Ghost is gone
				if (m_climbGhost != null) 
				{
					CameraFollow cam = FindObjectOfType<CameraFollow>();
					if (cam != null) cam.SetTarget(transform);
					Destroy(m_climbGhost);
				}
				
				return; // Stop processing
			}

			// if this instance is currently playable
			if (isCurrentlyPlayable)
			{
				if (isHurt)
				{
					// simple friction or air drag while hurt? 
					// for now just let physics handle the knockback force
					// maybe slow down x slightly if needed, but linear drag on RB is usually better.
					return;
				}

				// crouching logic
				if (enableCrouch && GameInput.Crouch() && isGrounded)
				{
					if (!isCrouching)
					{
						isCrouching = true;
						m_collider.size = new Vector2(m_originalColliderSize.x, m_originalColliderSize.y / 2f);
						m_collider.offset = new Vector2(m_originalColliderOffset.x, m_originalColliderOffset.y - (m_originalColliderSize.y / 4f));
					}
					moveInput /= crouchSpeedDivisor;
				}
				else if (isCrouching) // attempt to stand up
				{
					// Stay crouched if hurt (forced crouch from overhead hit)
					if (isHurt)
					{
						moveInput /= crouchSpeedDivisor;
					}
					// simple check: can strictly only stand up if not holding crouch. 
					// Ideally we check overhead, but for now just revert.
					else if(!GameInput.Crouch())
					{
						if (CanStand())
						{
							isCrouching = false;
							m_collider.size = m_originalColliderSize;
							m_collider.offset = m_originalColliderOffset;
						}
						else
						{
							moveInput /= crouchSpeedDivisor;
						}
					}
					else
					{
						// if still holding crouch button but in air, maybe stay crouched? 
						// Current requirement was just "crouching system". 
						// logic here: if button held, stay crouched. if released, stand up.
						// The if condition above handles "entering" crouch only on ground.
						// This else-if handles "staying" crouched or standing up.
						// If user holds crouch in air, isCrouching remains true if it was already true.
						moveInput /= crouchSpeedDivisor;
					}
				}

				// Attack Slowdown
				if (m_attackSlowdownTimer > 0f)
				{
					moveInput /= attackSpeedDivisor;
				}

				// horizontal movement
				if(m_wallJumping)
				{
					m_rb.linearVelocity = Vector2.Lerp(m_rb.linearVelocity, (new Vector2(moveInput * speed, m_rb.linearVelocity.y)), 1.5f * Time.fixedDeltaTime);
				}
				else
				{
					if(canMove && !m_wallGrabbing)
						m_rb.linearVelocity = new Vector2(moveInput * speed, m_rb.linearVelocity.y);
					else if(!canMove)
						m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
				}
				// better jump physics
				if (m_rb.linearVelocity.y < 0f)
				{
					m_rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
				}
				else if (m_rb.linearVelocity.y > 0f && !GameInput.JumpHeld())
				{
					m_rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
				}

				// Flipping
				if (!m_facingRight && moveInput > 0f)
					Flip();
				else if (m_facingRight && moveInput < 0f)
					Flip();


				// Dashing logic
				if (isDashing)
				{
					if (m_dashTime <= 0f)
					{
						isDashing = false;
						m_dashCooldown = dashCooldown;
						m_dashTime = startDashTime;
						m_rb.linearVelocity = Vector2.zero;
					}
					else
					{
						m_dashTime -= Time.deltaTime;
						if(m_facingRight)
							m_rb.linearVelocity = Vector2.right * dashSpeed;
						else
							m_rb.linearVelocity = Vector2.left * dashSpeed;
					}
				}

				// wall grab & climb
				// Calculate grab offset based on wall side
				Vector2 grabOffset = m_onRightWall ? grabRightOffset : grabLeftOffset;
				bool isClimbableWall = Physics2D.OverlapCircle((Vector2)transform.position + grabOffset, grabCheckRadius, whatIsClimbable);
				
				// Allow grab if falling OR if it's a climbable wall (can grab while jumping up)
				bool canGrab = m_onWall && !isGrounded && m_playerSide == m_onWallSide && !m_isLedgeClimbing;
				bool isFalling = m_rb.linearVelocity.y <= 0f;

				if(canGrab && (isFalling || isClimbableWall))
				{
					actuallyWallGrabbing = true;    // temporarily true, disabled below if climbing
					m_wallGrabbing = true;
					
					// Detect if this specific wall is climbable
					// Check the grab point (Right or Left offset depending on wall side)
					Vector2 checkPos = (Vector2)transform.position + grabOffset;
					// bool isClimbableWall already calculated above
					
					// DEBUG: Remove after testing
					// Debug.Log($"WallGrab: {m_onWall} | Side: {m_onWallSide} | Climbable: {isClimbableWall} | V-Input: {GameInput.VerticalRaw()}");

					if (isClimbableWall)
					{
						// --- CLIMBABLE WALL LOGIC ---
						float vInput = GameInput.VerticalRaw();
						
						// Ledge Check (Use combined mask!)
						float dir = m_onRightWall ? 1f : -1f;
						Vector2 ledgeCheckPos = new Vector2(transform.position.x + (dir * ledgeCheckOffset.x), transform.position.y + ledgeCheckOffset.y);
						// FIX: Use grabCheckRadius instead of 0.1f to strictly match wall detection reach.
						bool hitLedge = Physics2D.OverlapCircle(ledgeCheckPos, grabCheckRadius, combinedWallMask);

						// Logic:
						// If we are CLIMBING UP and there is NO WALL above us (hitLedge == false), we found the ledge top.
						if (vInput > 0f && !hitLedge)
						{
							Debug.Log($"[PlayerController] Ledge Climb Triggered! vInput: {vInput}, hitLedge: {hitLedge}");
							StartCoroutine(LedgeClimbRoutine());
						}
						else 
						{
							// Climbing Up/Down or Hanging
							m_isWallClimbing = true;
							actuallyWallGrabbing = false; // FIX: Disable generic "Wall Grab/Slide" animation to avoid conflict
							m_rb.gravityScale = 0f; // Disable gravity to hang/climb smoothly
							
							// Animation Speed Control handled in PlayerAnimator.cs
							// if (m_animator != null) ...
							
							if (vInput == 0f)
							{
								// HANG - Stop particles if any?
								m_rb.linearVelocity = Vector2.zero;
							}
							else
							{
								// CLIMB
								// FIX: Lock X to 0 to prevent breaking through wall
								m_rb.linearVelocity = new Vector2(0f, vInput * climbSpeed);
							}
						}
					}
					else
					{
						// --- GLIDE / NORMAL WALL LOGIC ---
						// No climbing allowed. Enforce Slide.
						m_isWallClimbing = false;
						m_rb.gravityScale = m_defaultGravity; // Ensure gravity applies (though typically we counter it for slide? or just let it fall?)
						// Standard wall slide mechanic usually throttles fall speed
						m_rb.linearVelocity = new Vector2(moveInput * speed, -slideSpeed);
					}

					m_wallStick = m_wallStickTime;
				} else
				{
					m_wallStick -= Time.deltaTime;
					actuallyWallGrabbing = false;
					m_isWallClimbing = false;
					if (m_wallStick <= 0f)
						m_wallGrabbing = false;
				}
				if (m_wallGrabbing && isGrounded)
					m_wallGrabbing = false;

				// enable/disable dust particles
				float playerVelocityMag = m_rb.linearVelocity.sqrMagnitude;
				if(m_dustParticle.isPlaying && playerVelocityMag == 0f)
				{
					m_dustParticle.Stop();
				}
				else if(!m_dustParticle.isPlaying && playerVelocityMag > 0f)
				{
					m_dustParticle.Play();
				}

			}
		}

		private void Update()
		{

			
			// horizontal input
			moveInput = GameInput.HorizontalRaw();

			HandleCameraLook();

			if (isGrounded)
			{
				m_extraJumps = extraJumpCount;
				// isHurt = false; // REMOVED: This was cancelling knockback instantly on ground hits.
				// We now rely purely on m_hurtTimer to reset isHurt.
			}

			// grounded remember offset (for more responsive jump)
			m_groundedRemember -= Time.deltaTime;
			if (isGrounded)
				m_groundedRemember = coyoteTime;

			if (!isCurrentlyPlayable) return;
			
			// Ledge Drop Logic
			// if (isGrounded && GameInput.VerticalRaw() < -0.8f && !m_isLedgeClimbing)
			// {
			// 	CheckLedgeDrop();
			// }

			if (isHurt)
			{
				// Death Check on Landing
				if (currentHealth <= 0 && !m_hasTriggeredDeath)
				{
					// If grounded and NOT moving upwards (falling or standing)
					if (isGrounded && m_rb.linearVelocity.y <= 0.1f)
					{
						m_hasTriggeredDeath = true;
						Die();
					}
				}

				m_hurtTimer -= Time.deltaTime;
				if (m_hurtTimer <= 0f && currentHealth > 0) // Only recover if alive
					isHurt = false;
				else
					return; // disable input
			}

			// if not currently dashing and hasn't already dashed in air once
			if (!isDashing && !m_hasDashedInAir && m_dashCooldown <= 0f)
			{
				// dash input (left shift)
				if (enableDash && !isCrouching && !m_wallGrabbing && GameInput.Dash())
				{
					isDashing = true;
					m_dashTime = startDashTime; // FIX: Ensure full dash duration is reset on start
					// dash effect
					PoolManager.instance.ReuseObject(dashEffect, transform.position, Quaternion.identity);
					// if player in air while dashing
					if(!isGrounded)
					{
						m_hasDashedInAir = true;
					}
					// dash logic is in FixedUpdate
				}
			}
			m_dashCooldown -= Time.deltaTime;

			// Attack Input
			if (enableAttack && !isCrouching && !m_wallGrabbing && GameInput.Attack())
			{
				// Check for air attack capability
				if (enableAirAttack || isGrounded)
				{
					isAttacking = true;
					m_attackSlowdownTimer = attackSlowdownDuration;
					CheckAttackHitbox(); // Instant hit for now, can be moved to Animation Event later
				}
			}
			else
			{
				isAttacking = false;
				m_attackSlowdownTimer -= Time.deltaTime;
			}

			// UI Updates
			if(showHealthBar) UpdateHealthBarPosition();
			
			// if has dashed in air once but now grounded
			if (m_hasDashedInAir && isGrounded)
				m_hasDashedInAir = false;
			
			// Jumping
			if (enableJump && !isCrouching && GameInput.Jump())
			{
				m_jumpBufferCounter = jumpBufferTime;
			}
			else
			{
				m_jumpBufferCounter -= Time.deltaTime;
			}

			if(m_jumpBufferCounter > 0f && m_extraJumps > 0 && !isGrounded && !m_wallGrabbing && enableDoubleJump)	// extra jumping
			{
				m_rb.linearVelocity = new Vector2(m_rb.linearVelocity.x, m_extraJumpForce); ;
				m_extraJumps--;
				m_jumpBufferCounter = 0f;
				// jumpEffect
				PoolManager.instance.ReuseObject(jumpEffect, groundCheck.position, Quaternion.identity);
			}
			else if(m_jumpBufferCounter > 0f && (isGrounded || m_groundedRemember > 0f))	// normal single jumping
			{
				m_rb.linearVelocity = new Vector2(m_rb.linearVelocity.x, jumpForce);
				m_jumpBufferCounter = 0f;
				// jumpEffect
				PoolManager.instance.ReuseObject(jumpEffect, groundCheck.position, Quaternion.identity);
			}
			else if(m_jumpBufferCounter > 0f && m_wallGrabbing && moveInput!=m_onWallSide )		// wall jumping off the wall
			{
				m_wallGrabbing = false;
				m_isWallClimbing = false; // FIX: Reset climbing state on jump
				m_isLedgeClimbing = false;
				m_wallJumping = true;
				m_jumpBufferCounter = 0f;
				Debug.Log("Wall jumped");
				if (m_playerSide == m_onWallSide)
					Flip();
				m_rb.AddForce(new Vector2(-m_onWallSide * wallJumpForce.x, wallJumpForce.y), ForceMode2D.Impulse);
			}
			else if(m_jumpBufferCounter > 0f && m_wallGrabbing && moveInput != 0 && (moveInput == m_onWallSide))      // wall climbing jump
			{
				m_wallGrabbing = false;
				m_isWallClimbing = false; // FIX: Reset climbing state on jump
				m_isLedgeClimbing = false;
				m_wallJumping = true;
				m_jumpBufferCounter = 0f;
				Debug.Log("Wall climbed");
				if (m_playerSide == m_onWallSide)
					Flip();
				m_rb.AddForce(new Vector2(-m_onWallSide * wallClimbForce.x, wallClimbForce.y), ForceMode2D.Impulse);
			}

		}

		private void HandleCameraLook()
		{
			if (m_cam == null) return;

			// Only allow looking if grounded and not moving horizontally
			// Increased threshold to 0.25f to be more forgiving on Gamepad stick drift/angled presses
			if (isGrounded && Mathf.Abs(moveInput) < 0.25f && !m_wallGrabbing && !isDashing && !isAttacking)
			{
				float vInput = GameInput.VerticalRaw();

				if (Mathf.Abs(vInput) > 0.5f)
				{
					// Counting down
					m_lookTimer -= Time.deltaTime;
					if (m_lookTimer <= 0f)
					{
						float yOffset = (vInput > 0) ? lookDistance : -lookDistance;
						m_cam.SetLookOffset(new Vector3(0f, yOffset, 0f));
					}
				}
				else
				{
					// Reset
					ResetCameraLook();
				}
			}
			else
			{
				// Moving or in air, reset immediately
				ResetCameraLook();
			}
		}

		private void ResetCameraLook()
		{
			m_lookTimer = lookDelay;
			if (m_cam != null) m_cam.SetLookOffset(Vector3.zero);
		}

		void Flip()
		{
			m_facingRight = !m_facingRight;
			Vector3 scale = transform.localScale;
			scale.x *= -1;
			transform.localScale = scale;

			// Fix Health Bar Flip
			if (m_healthBarRoot != null)
			{
				Vector3 barScale = m_healthBarRoot.localScale;
				barScale.x *= -1;
				m_healthBarRoot.localScale = barScale;
			}
		}

		public void TakeDamage(Vector2 sourcePosition)
		{
			if (isHurt || currentHealth <= 0) return;

			// Decrease Health
			currentHealth--;
			
			// Critical Fix: If dead, immediately stop climbing so we fall.
			// This allows the "Wait for Landing" death logic to actually happen (otherwise we hang forever).
			if (currentHealth <= 0)
			{
				m_isWallClimbing = false;
				m_isLedgeClimbing = false;
				m_wallGrabbing = false;
				m_rb.gravityScale = m_defaultGravity > 0.1f ? m_defaultGravity : 3f; // Ensure gravity
				StopAllCoroutines(); // Stop any ledge climb routine
				
				// Also destroy ghost if it exists
				if (m_climbGhost != null) 
				{
					if (Camera.main && Camera.main.GetComponent<CameraFollow>()) 
						Camera.main.GetComponent<CameraFollow>().SetTarget(transform);
					Destroy(m_climbGhost);
				}
			}
			
			// Apply Hurt state immediately to disable inputs / enable physics (even if dead)

			isHurt = true;
			m_hurtTimer = hurtDuration;
			
			// Face the source (Face TOWARDS the source)
			if (sourcePosition.x > transform.position.x && !m_facingRight)
				Flip();
			else if (sourcePosition.x < transform.position.x && m_facingRight)
				Flip();
			
			// Visual Flash
			if (m_spriteRenderer != null)
				StartCoroutine(FlashRoutine());

			// Cancel Dash so physics can take over
			isDashing = false;

			// Zero out velocity before knockback
			m_rb.linearVelocity = Vector2.zero;
			
			// Calculate Knockback direction
			// Default: Away and Up
			int dir = sourcePosition.x > transform.position.x ? -1 : 1;
			Vector2 force = new Vector2(dir * knockbackForce.x, knockbackForce.y);

			// Vertical Checks
			float yDiff = sourcePosition.y - transform.position.y;
			if (yDiff > 1.0f) // Hit from above ~roughly
			{
				// Force crouch if enabled
				if (enableCrouch)
				{
					isCrouching = true;
					m_collider.size = new Vector2(m_originalColliderSize.x, m_originalColliderSize.y / 2f);
					m_collider.offset = new Vector2(m_originalColliderOffset.x, m_originalColliderOffset.y - (m_originalColliderSize.y / 4f));
				}
				// Knockback slightly down or just Horizontal? 
				// "bounce slightly up and in the other direction" was for UNDERNEATH.
				// "crouch sprites and also react in a direction" for ABOVE.
				// FIX: Don't push down, it increases friction. Use 1f (small hop) or 0f.
				force = new Vector2(dir * knockbackForce.x, 1f); 
			}
			else if (yDiff < -1.0f) // Hit from below
			{
				// "bounce slightly up"
				force = new Vector2(dir * knockbackForce.x, knockbackForce.y * 1.2f);
			}

			m_rb.AddForce(force, ForceMode2D.Impulse);

			// Update UI
			if (showHealthBar)
				UpdateHealthBar();
			
			// Note: We do NOT die here. We wait for Update() -> Landing to trigger Die().
		}

		private void CreateHealthBar()
		{
			// Create Root
			GameObject root = new GameObject("HealthBar");
			root.transform.SetParent(transform);
			root.transform.localPosition = healthBarOffset;
			m_healthBarRoot = root.transform;

			// Generate Texture
			Texture2D tex = new Texture2D(1, 1);
			tex.filterMode = FilterMode.Point;
			tex.SetPixel(0, 0, Color.white);
			tex.Apply();
			
			// PPU 1 for correct sizing
			Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

			// Background (Red)
			GameObject bg = new GameObject("Background");
			bg.transform.SetParent(root.transform);
			bg.transform.localPosition = Vector3.zero;
			bg.transform.localScale = new Vector3(healthBarSize.x, healthBarSize.y, 1f);
			SpriteRenderer bgSr = bg.AddComponent<SpriteRenderer>();
			bgSr.sprite = sprite;
			bgSr.color = Color.red;
			bgSr.sortingLayerName = "UI"; // Render on top of everything (Gameplay Over, Characters, etc.)
			bgSr.sortingOrder = 0; // Reset order, layer priority handles it (or keep high within UI if needed)

			// Fill (Green)
			GameObject fill = new GameObject("Fill");
			fill.transform.SetParent(root.transform);
			
			// Left-Pivoted Sprite
			Sprite leftPivotSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);

			fill.transform.localPosition = new Vector3(-healthBarSize.x / 2f, 0f, 0f);
			fill.transform.localScale = new Vector3(healthBarSize.x, healthBarSize.y, 1f);
			
			m_healthBarFill = fill.transform;
			SpriteRenderer fillSr = fill.AddComponent<SpriteRenderer>();
			fillSr.sprite = leftPivotSprite;
			fillSr.color = Color.green;
			fillSr.sortingLayerName = "UI";
			fillSr.sortingOrder = 1; // Above background
		}



		private void UpdateHealthBar()
		{
			if (m_healthBarFill != null)
			{
				float pct = Mathf.Clamp01((float)currentHealth / maxHealth);
				Vector3 s = m_healthBarFill.localScale;
				s.x = healthBarSize.x * pct;
				m_healthBarFill.localScale = s;
			}
		}

		private void UpdateHealthBarPosition()
		{
			if (m_healthBarRoot == null) return;



			// Detect Crouch Start for Delay
			if (isCrouching && !m_wasCrouching)
			{
				m_crouchDelayTimer = crouchUiDelay;
			}
			m_wasCrouching = isCrouching;

			Vector2 targetLocalPos = healthBarOffset; // Default standing

			if (isCrouching)
			{
				// Process Delay
				if (m_crouchDelayTimer > 0f)
				{
					m_crouchDelayTimer -= Time.deltaTime;
					targetLocalPos = healthBarOffset; // Stay standing during delay
				}
				else
				{
					targetLocalPos = crouchHealthBarOffset;
				}
			}
			// Ledge Climb UI offset removed as requested

			// Smoothly interpolate with configurable speed
			m_healthBarRoot.localPosition = Vector3.Lerp(m_healthBarRoot.localPosition, targetLocalPos, healthBarSmoothSpeed * Time.deltaTime);
		}



		void CalculateSides()
		{
			if (m_onRightWall)
				m_onWallSide = 1;
			else if (m_onLeftWall)
				m_onWallSide = -1;
			else
				m_onWallSide = 0;

			if (m_facingRight)
				m_playerSide = 1;
			else
				m_playerSide = -1;
		}

		// Called when stomping an enemy
		public void Bounce()
		{
			m_rb.linearVelocity = new Vector2(m_rb.linearVelocity.x, bounceForce);
			m_extraJumps = extraJumpCount; // Optional: Reset jumps on stomp? standard platformer mechanic
		}

		private void CheckAttackHitbox()
		{
			if (attackPoint == null) return;

			Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);

			foreach (Collider2D enemyCollider in hitEnemies)
			{
				// We need a common interface or check for specific script
				// For now, looking for SimplePatrolEnemy specifically or generic interface
				var enemy = enemyCollider.GetComponent<Enemies.SimplePatrolEnemy>();
				if (enemy != null)
				{
					enemy.TakeDamage(attackDamage);
					// Calculate knockback direction
					float dir = Mathf.Sign(enemyCollider.transform.position.x - transform.position.x);
					enemy.ApplyKnockback(new Vector2(dir * attackKnockback, 2f)); // minimal y lift
				}
			}

		}

		private System.Collections.IEnumerator LedgeClimbRoutine()
		{
			if (m_isLedgeClimbing) yield break;

			Debug.Log("Ledge Climb Triggered");
			m_isLedgeClimbing = true;
			isCurrentlyPlayable = false; // Disable Input
			m_rb.linearVelocity = Vector2.zero; // Freeze
			m_rb.gravityScale = 0f; // Disable gravity
			if (m_collider != null) m_collider.enabled = false; // Prevent clipping
			
			// Setup Teleport Target
			Vector3 startPos = transform.position;
			float dir = m_facingRight ? 1f : -1f;
			Vector3 targetPos = startPos + new Vector3(dir * ledgeTeleportOffset.x, ledgeTeleportOffset.y, 0f);

			// --- GHOST CAMERA LOGIC ---
			// Create a ghost target for the camera to follow so it moves smoothly while player mimics movement
			m_climbGhost = new GameObject("ClimbGhostCameraTarget");
			m_climbGhost.transform.position = startPos;
			
			CameraFollow cam = FindFirstObjectByType<CameraFollow>();
			if (cam != null)
			{
				cam.SetTarget(m_climbGhost.transform);
			}
			// --------------------------

			// Wait for the animator to pick up the change
			yield return null; 
			yield return null;

			// Wait for Animation to Finish
			float safetyTimer = 0f;
			while (m_animator != null && safetyTimer < 2.5f)
			{
				AnimatorStateInfo stateInfo = m_animator.GetCurrentAnimatorStateInfo(0);
				
				if (stateInfo.IsName("LedgeClimb"))
				{
					// Move Ghost to match would-be player position (Linear Lerp for smooth camera)
					float t = Mathf.Clamp01(stateInfo.normalizedTime);
					m_climbGhost.transform.position = Vector3.Lerp(startPos, targetPos, t);

					if (stateInfo.normalizedTime >= 1.0f) // Animation finished
						break;
				}
				
				safetyTimer += Time.deltaTime;
				yield return null;
			}
			
			// Teleport Up and Over
			transform.position = targetPos;
			
			// Restore Camera to Player
			if (cam != null)
			{
				cam.SetTarget(transform);
			}
			if (m_climbGhost != null) Destroy(m_climbGhost);

			// Force Idle Immediately to prevent single-frame flash of "high" LedgeClimb frame
			if (m_animator != null)
			{
				m_animator.Play("Idle", 0, 0f); 
			}

			// Restore
			if (m_collider != null) m_collider.enabled = true;
			m_rb.gravityScale = m_defaultGravity; // Restore cached default
			
			m_isLedgeClimbing = false;
			isCurrentlyPlayable = true;
		}

		private void CheckLedgeDrop()
		{
			// Check if we are at an edge and looking out
			float dir = m_facingRight ? 1f : -1f;
			Vector2 scanOrigin = (Vector2)transform.position + new Vector2(dir * 0.5f, 0f);
			
			// 1. Check if there is ground AHEAD (should be false)
			bool groundAhead = Physics2D.Raycast(scanOrigin, Vector2.down, 1f, whatIsGround);
			
			if (!groundAhead)
			{
				// 2. Check if there is a Climbable Wall BELOW the edge (should be true)
				// Look down and slightly back towards the wall
				Vector2 wallCheckOrigin = scanOrigin + new Vector2(0f, -1f); 
				// Actually, if we walk off, the wall is "behind" the drop point relative to facing, 
				// or directly below if it's a vertical drop.
				// Let's check for Climbable roughly where we would hang.
				
				bool wallBelow = Physics2D.OverlapCircle(wallCheckOrigin, 0.5f, whatIsClimbable);
				
				if (wallBelow)
				{
					StartCoroutine(LedgeDropRoutine());
				}
			}
		}

		private System.Collections.IEnumerator LedgeDropRoutine()
		{
			m_isLedgeClimbing = true;
			isCurrentlyPlayable = false;
			m_rb.linearVelocity = Vector2.zero;
			
			// Walk off slightly
			float dir = m_facingRight ? 1f : -1f;
			transform.position += new Vector3(dir * 0.4f, 0f, 0f);
			
			yield return new WaitForSeconds(0.1f);
			
			// Drop Down
			transform.position += new Vector3(0f, -1.0f, 0f);
			
			// Turn around to face the wall
			Flip();
			
			m_isLedgeClimbing = false;
			isCurrentlyPlayable = true;
		}

		private bool CanStand()
		{
			// Check if we can maximize the collider size
			Vector2 center = (Vector2)transform.position + m_originalColliderOffset + new Vector2(0f, m_originalColliderSize.y / 4f);
			Vector2 size = new Vector2(m_originalColliderSize.x, m_originalColliderSize.y / 2f);
			size *= 0.9f;
			return !Physics2D.OverlapBox(center, size, 0f, whatIsGround | whatIsWall);
		}
		
		private System.Collections.IEnumerator FlashRoutine()
		{
			// With Shader "SwampPreachers/SpriteFlash" check "swamp-preachers-game\Assets\Shaders\SpriteFlash.shader"
			// Property is _FlashAmount
			
			float elapsed = 0f;
			while (elapsed < hurtDuration)
			{
				m_spriteRenderer.material.SetFloat("_FlashAmount", 1f);
				yield return new WaitForSeconds(0.1f);
				m_spriteRenderer.material.SetFloat("_FlashAmount", 0f);
				yield return new WaitForSeconds(0.1f);
				elapsed += 0.2f;
			}
			m_spriteRenderer.material.SetFloat("_FlashAmount", 0f);
		}

		private void OnDrawGizmos()
		{
			Gizmos.color = Color.red;
			if (groundCheck != null)
				Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
			
			Gizmos.color = Color.blue;
			Gizmos.DrawWireSphere((Vector2)transform.position + grabRightOffset, grabCheckRadius);
			Gizmos.DrawWireSphere((Vector2)transform.position + grabLeftOffset, grabCheckRadius);
			
			// Ledge Check Gizmo
			Vector2 rightLedge = (Vector2)transform.position + new Vector2(ledgeCheckOffset.x, ledgeCheckOffset.y);
			Vector2 leftLedge = (Vector2)transform.position + new Vector2(-ledgeCheckOffset.x, ledgeCheckOffset.y);
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireSphere(rightLedge, 0.1f);
			Gizmos.DrawWireSphere(leftLedge, 0.1f);

			Gizmos.color = Color.yellow;
			if (attackPoint != null)
			{
				Gizmos.DrawWireSphere(attackPoint.position, attackRange);
			}
		}

		private GameObject m_climbGhost;

		private void Die()
		{
			// Cleanup Coroutines first (Stops LedgeClimb floating loop)
			StopAllCoroutines();

			// RESET PHYSICS (Fixes floating if died on ledge)
			m_rb.gravityScale = m_defaultGravity;
			if (m_collider != null) m_collider.enabled = true; // Ensure we fall to ground
			
			// Use default death logic
			isCurrentlyPlayable = false;
			m_rb.linearVelocity = Vector2.zero;
			isDashing = false;

			// CLEANUP GHOST CAMERA
			if (m_climbGhost != null)
			{
				CameraFollow cam = FindFirstObjectByType<CameraFollow>();
				if (cam != null) cam.SetTarget(transform); // Look back at dead player
				Destroy(m_climbGhost);
			}

			// RESET FLAGS (Critical to ensure FixedUpdate doesn't bail out or skip gravity)
			m_isLedgeClimbing = false;
			m_isWallClimbing = false;
			m_wallGrabbing = false;

			if (m_animator != null)
			{
				m_animator.SetTrigger("Die");
			}

			Debug.Log("Player Died. Reloading scene...");
			StartCoroutine(ReloadScene()); // Restart reload coroutine since we stopped all
		}

		private System.Collections.IEnumerator ReloadScene()
		{
			yield return new WaitForSeconds(deathDelay);
			SceneManager.LoadScene(SceneManager.GetActiveScene().name);
		}
	}
}
