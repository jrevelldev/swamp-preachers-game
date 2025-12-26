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

		[Header("UI")]
		[SerializeField] private bool showHealthBar = true;
		[SerializeField] private Vector2 healthBarOffset = new Vector2(0f, 0.8f);
		[SerializeField] private Vector2 healthBarSize = new Vector2(1f, 0.15f);

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
		[SerializeField] private LayerMask whatIsWall;

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

		[Header("Wall grab & jump")]
		[Tooltip("Right offset of the wall detection sphere")]
		public Vector2 grabRightOffset = new Vector2(0.16f, 0f);
		public Vector2 grabLeftOffset = new Vector2(-0.16f, 0f);
		public float grabCheckRadius = 0.24f;
		public float slideSpeed = 2.5f;
		public Vector2 wallJumpForce = new Vector2(10.5f, 18f);
		public Vector2 wallClimbForce = new Vector2(4f, 14f);

		private Rigidbody2D m_rb;
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
			m_collider = GetComponent<BoxCollider2D>();
			m_originalColliderSize = m_collider.size;
			m_originalColliderOffset = m_collider.offset;
			m_dustParticle = GetComponentInChildren<ParticleSystem>();
			m_spriteRenderer = GetComponentInChildren<SpriteRenderer>();
			m_animator = GetComponentInChildren<Animator>();
			currentHealth = maxHealth;
			
			// Auto-setup shader if possible
			// Assuming the user didn't assign a material manually, we might want to ensure we can flash.
			// But for now, we rely on the shader property being present or we swap shader.
			// Ideally, we just find the shader.
			if (m_spriteRenderer != null)
			{
				m_spriteRenderer.material.shader = Shader.Find("SwampPreachers/SpriteFlash");
			}

			if (showHealthBar)
				CreateHealthBar();
		}

		private void FixedUpdate()
		{
			// check if grounded
			isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, whatIsGround);
			var position = transform.position;
			// check if on wall
			m_onWall = Physics2D.OverlapCircle((Vector2)position + grabRightOffset, grabCheckRadius, whatIsWall)
			          || Physics2D.OverlapCircle((Vector2)position + grabLeftOffset, grabCheckRadius, whatIsWall);
			m_onRightWall = Physics2D.OverlapCircle((Vector2)position + grabRightOffset, grabCheckRadius, whatIsWall);
			m_onLeftWall = Physics2D.OverlapCircle((Vector2)position + grabLeftOffset, grabCheckRadius, whatIsWall);

			// calculate player and wall sides as integers
			CalculateSides();

			if((m_wallGrabbing || isGrounded) && m_wallJumping)
			{
				m_wallJumping = false;
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
				if (enableCrouch && InputSystem.Crouch() && isGrounded)
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
					else if(!InputSystem.Crouch())
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
				else if (m_rb.linearVelocity.y > 0f && !InputSystem.JumpHeld())
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

				// wall grab
				if(m_onWall && !isGrounded && m_rb.linearVelocity.y <= 0f && m_playerSide == m_onWallSide)
				{
					actuallyWallGrabbing = true;    // for animation
					m_wallGrabbing = true;
					m_rb.linearVelocity = new Vector2(moveInput * speed, -slideSpeed);
					m_wallStick = m_wallStickTime;
				} else
				{
					m_wallStick -= Time.deltaTime;
					actuallyWallGrabbing = false;
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
			CheckDebugHurt();
			
			// horizontal input
			moveInput = InputSystem.HorizontalRaw();

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
				if (enableDash && !isCrouching && !m_wallGrabbing && InputSystem.Dash())
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
			if (enableAttack && !isCrouching && !m_wallGrabbing && InputSystem.Attack())
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
			
			// if has dashed in air once but now grounded
			if (m_hasDashedInAir && isGrounded)
				m_hasDashedInAir = false;
			
			// Jumping
			if (enableJump && !isCrouching && InputSystem.Jump())
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
				m_wallJumping = true;
				m_jumpBufferCounter = 0f;
				Debug.Log("Wall climbed");
				if (m_playerSide == m_onWallSide)
					Flip();
				m_rb.AddForce(new Vector2(-m_onWallSide * wallClimbForce.x, wallClimbForce.y), ForceMode2D.Impulse);
			}

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
			bgSr.sortingOrder = 100;

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
			fillSr.sortingOrder = 101;
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

		// Debug Inputs
		private void CheckDebugHurt()
		{
			if (UnityEngine.InputSystem.Keyboard.current == null) return;
			
			if (UnityEngine.InputSystem.Keyboard.current.hKey.wasPressedThisFrame)
				TakeDamage((Vector2)transform.position + Vector2.left); // Hit from left
			if (UnityEngine.InputSystem.Keyboard.current.jKey.wasPressedThisFrame)
				TakeDamage((Vector2)transform.position + Vector2.right); // Hit from right
			if (UnityEngine.InputSystem.Keyboard.current.uKey.wasPressedThisFrame)
				TakeDamage((Vector2)transform.position + Vector2.up * 2f); // Hit from above
			if (UnityEngine.InputSystem.Keyboard.current.nKey.wasPressedThisFrame)
				TakeDamage((Vector2)transform.position + Vector2.down * 2f); // Hit from below
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
			
			Gizmos.color = Color.yellow;
			if (attackPoint != null)
			{
				Gizmos.DrawWireSphere(attackPoint.position, attackRange);
			}
		}

		private void Die()
		{
			isCurrentlyPlayable = false;
			m_rb.linearVelocity = Vector2.zero;
			isDashing = false;
			
			// Disable physics/collider to prevent further hits
			// m_rb.simulated = false; // We might want gravity to keep body on ground? 
			// Instead let's just make sure we don't move and ignore inputs (already done by isCurrentlyPlayable)
			// But maybe disable collider so enemies walk past?
			// m_collider.enabled = false; 

			if (m_animator != null)
			{
				m_animator.SetTrigger("Die");
			}

			Debug.Log("Player Died. Reloading scene...");
			StartCoroutine(ReloadScene());
		}

		private System.Collections.IEnumerator ReloadScene()
		{
			yield return new WaitForSeconds(deathDelay);
			SceneManager.LoadScene(SceneManager.GetActiveScene().name);
		}
	}
}
