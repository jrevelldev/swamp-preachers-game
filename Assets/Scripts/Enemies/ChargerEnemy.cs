using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SwampPreachers.Enemies
{
	[RequireComponent(typeof(Rigidbody2D))]
	[RequireComponent(typeof(Collider2D))]
	[RequireComponent(typeof(EnemyStats))]
	public class ChargerEnemy : MonoBehaviour
	{
		public enum State
		{
			Patrol,
			Windup,
			Charging,
			Recoil,
			Stunned,
			Dead
		}

		[Header("Patrol Settings")]
		[Tooltip("Optional waypoints. If empty, uses autonomous ground patrol.")]
		[SerializeField] private Transform[] patrolPoints;
		[SerializeField] private float patrolSpeed = 2.5f;
		[SerializeField] private float waitTime = 0.5f;

		[Header("Autonomous Ground Patrol")]
		[SerializeField] private float wallCheckDistance = 0.4f;
		[SerializeField] private float ledgeCheckForwardOffset = 0.5f;
		[SerializeField] private float ledgeCheckDownDistance = 1.0f;

		[Header("Charger Detection")]
		[SerializeField] private float detectionRange = 8f;
		[SerializeField] private float detectionBoxHeight = 1.6f;
		[SerializeField] private Vector2 detectionOffset = new Vector2(0f, 0.2f);
		[SerializeField] private LayerMask playerLayer;
		[SerializeField] private LayerMask obstacleLayer;

		[Header("Charge Behavior")]
		[SerializeField] private float chargeSpeed = 11f;
		[SerializeField] private float chargeWindupTime = 0.25f;
		[SerializeField] private float maxChargeDuration = 2.5f;
		[SerializeField] private float stunDurationSelf = 2.0f;
		[SerializeField] private float tiredStunDuration = 1.0f;
		[SerializeField] private float chargeCooldown = 1.0f;
		[SerializeField] private Vector2 recoilForce = new Vector2(5f, 3.5f);

		[Header("Visual Feedback")]
		[SerializeField] private Color windupColor = new Color(1f, 0.35f, 0.35f, 1f);
		[SerializeField] private Color stunnedColor = new Color(0.6f, 0.65f, 0.9f, 1f);

		private State m_state = State.Patrol;
		public State CurrentState => m_state;

		private float m_facingDirection = 1f;
		private List<Transform> m_validPatrolPoints = new List<Transform>();
		private int m_currentPointIndex = 0;
		private float m_waitTimer = 0f;
		private bool m_isWaiting = false;

		private float m_stuckTimer = 0f;
		private float m_lastPosX = 0f;

		private float m_chargeTimer = 0f;
		private float m_cooldownTimer = 0f;
		private float m_recoilTimer = 0f;

		private Rigidbody2D m_rb;
		private Collider2D m_col;
		private Animator m_anim;
		private SpriteRenderer m_spriteRenderer;
		private EnemyStats m_stats;
		private Color m_originalColor = Color.white;

		private Coroutine m_currentRoutine;

		private static readonly int SpeedHash = Animator.StringToHash("Speed");
		private static readonly int ChargeHash = Animator.StringToHash("Charge");
		private static readonly int StunnedHash = Animator.StringToHash("IsStunned");

		private bool HasParameter(int paramHash)
		{
			if (m_anim == null || m_anim.runtimeAnimatorController == null) return false;
			foreach (var p in m_anim.parameters)
			{
				if (p.nameHash == paramHash) return true;
			}
			return false;
		}

		private void SafeSetBool(int hash, bool value)
		{
			if (HasParameter(hash))
			{
				m_anim.SetBool(hash, value);
			}
		}

		private void SafeSetFloat(int hash, float value)
		{
			if (HasParameter(hash))
			{
				m_anim.SetFloat(hash, value);
			}
		}

		private void Awake()
		{
			m_rb = GetComponent<Rigidbody2D>();
			m_col = GetComponent<Collider2D>();
			m_stats = GetComponent<EnemyStats>();
			m_anim = GetComponent<Animator>();
			if (m_anim == null) m_anim = GetComponentInChildren<Animator>();
			m_spriteRenderer = GetComponentInChildren<SpriteRenderer>();
			if (m_spriteRenderer != null) m_originalColor = m_spriteRenderer.color;
		}

		private void Start()
		{
			// Ensure enemy layer is set to Enemy
			int enemyLayer = LayerMask.NameToLayer("Enemy");
			if (enemyLayer != -1 && (gameObject.layer == 0 || gameObject.layer == LayerMask.NameToLayer("Default")))
			{
				gameObject.layer = enemyLayer;
			}

			// Validate and filter patrol points
			m_validPatrolPoints.Clear();
			if (patrolPoints != null)
			{
				foreach (var p in patrolPoints)
				{
					if (p != null) m_validPatrolPoints.Add(p);
				}
			}

			// Validate Layers
			if (playerLayer == 0) playerLayer = LayerMask.GetMask("Player");
			if (obstacleLayer == 0) obstacleLayer = LayerMask.GetMask("Ground", "Default");

			// Initial facing direction
			m_facingDirection = transform.localScale.x < 0 ? -1f : 1f;
			TurnToFace(m_facingDirection);

			m_lastPosX = transform.position.x;
		}

		private void Update()
		{
			if (m_cooldownTimer > 0f)
			{
				m_cooldownTimer -= Time.deltaTime;
			}
		}

		private void FixedUpdate()
		{
			if (m_state == State.Dead) return;

			if (m_stats != null && m_stats.IsDead)
			{
				OnDeath();
				return;
			}

			// If externally stunned by stats from player attack knockback
			if (m_stats != null && m_stats.IsStunned && m_state != State.Stunned && m_state != State.Recoil)
			{
				InterruptToStun(0.8f);
				return;
			}

			switch (m_state)
			{
				case State.Patrol:
					HandlePatrol();
					CheckForPlayer();
					break;

				case State.Windup:
					m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
					break;

				case State.Charging:
					HandleCharging();
					break;

				case State.Recoil:
					// Allow physical impulse to push enemy backwards without overriding velocity.x
					m_recoilTimer -= Time.fixedDeltaTime;
					if (m_recoilTimer <= 0f)
					{
						m_state = State.Stunned;
					}
					break;

				case State.Stunned:
					m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
					break;
			}

			SafeSetFloat(SpeedHash, Mathf.Abs(m_rb.linearVelocity.x));
		}

		private void HandlePatrol()
		{
			if (m_validPatrolPoints != null && m_validPatrolPoints.Count >= 2)
			{
				// Waypoint Patrol
				if (m_isWaiting)
				{
					m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
					m_waitTimer -= Time.fixedDeltaTime;
					if (m_waitTimer <= 0f)
					{
						m_isWaiting = false;
						m_currentPointIndex = (m_currentPointIndex + 1) % m_validPatrolPoints.Count;
					}
					return;
				}

				Transform target = m_validPatrolPoints[m_currentPointIndex];
				if (target != null)
				{
					float dist = Mathf.Abs(transform.position.x - target.position.x);
					if (dist < 0.25f)
					{
						StopAndTurnAround();
					}
					else
					{
						float dirX = Mathf.Sign(target.position.x - transform.position.x);
						TurnToFace(dirX);
						m_rb.linearVelocity = new Vector2(dirX * patrolSpeed, m_rb.linearVelocity.y);
					}
				}
			}
			else
			{
				// Autonomous Ground Patrol
				if (m_isWaiting)
				{
					m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
					m_waitTimer -= Time.fixedDeltaTime;
					if (m_waitTimer <= 0f)
					{
						m_isWaiting = false;
						TurnToFace(-m_facingDirection);
						m_lastPosX = transform.position.x;
						m_stuckTimer = 0f;
					}
					return;
				}

				Bounds colBounds = m_col != null ? m_col.bounds : new Bounds(transform.position, new Vector3(0.8f, 0.8f, 0f));
				Vector2 boxCenter = colBounds.center;
				// Cover 80% of body height to detect low walls AND low crouch ceilings without scraping floor
				Vector2 boxSize = new Vector2(0.1f, colBounds.size.y * 0.8f);
				Vector2 direction = Vector2.right * m_facingDirection;
				float halfWidth = colBounds.extents.x;

				// 1. Full height BoxCast to detect low walls, full walls, and low ceilings
				RaycastHit2D wallHit = Physics2D.BoxCast(boxCenter, boxSize, 0f, direction, halfWidth + wallCheckDistance, obstacleLayer);
				bool hitWall = (wallHit.collider != null && wallHit.collider != m_col);

				// 2. Ledge check (cliff / edge detection ahead)
				Vector2 ledgeOrigin = (Vector2)transform.position + Vector2.up * 0.2f + direction * (halfWidth + ledgeCheckForwardOffset);
				RaycastHit2D groundHit = Physics2D.Raycast(ledgeOrigin, Vector2.down, ledgeCheckDownDistance, obstacleLayer);
				bool reachedLedge = (groundHit.collider == null);

				if (hitWall || reachedLedge)
				{
					StopAndTurnAround();
					return;
				}

				// 3. Anti-Stuck detection: if trying to move but blocked physically by geometry
				if (Mathf.Abs(transform.position.x - m_lastPosX) < 0.02f)
				{
					m_stuckTimer += Time.fixedDeltaTime;
					if (m_stuckTimer >= 0.3f)
					{
						StopAndTurnAround();
						return;
					}
				}
				else
				{
					m_stuckTimer = 0f;
					m_lastPosX = transform.position.x;
				}

				m_rb.linearVelocity = new Vector2(m_facingDirection * patrolSpeed, m_rb.linearVelocity.y);
			}
		}

		private void StopAndTurnAround()
		{
			m_isWaiting = true;
			m_waitTimer = waitTime;
			m_stuckTimer = 0f;
			m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
		}

		private void CheckForPlayer()
		{
			if (m_cooldownTimer > 0f) return;

			Vector2 origin = (Vector2)transform.position + detectionOffset;
			Vector2 boxSize = new Vector2(0.2f, detectionBoxHeight);
			Vector2 direction = Vector2.right * m_facingDirection;

			// BoxCast forward covering full player height (standing and crouching)
			RaycastHit2D hit = Physics2D.BoxCast(origin, boxSize, 0f, direction, detectionRange, playerLayer);

			if (hit.collider != null && hit.collider != m_col)
			{
				float halfWidth = m_col != null ? m_col.bounds.extents.x : 0.4f;
				Vector2 sightOrigin = origin + direction * (halfWidth + 0.05f);

				// Linecast to verify clear line of sight
				RaycastHit2D blockHit = Physics2D.Linecast(sightOrigin, hit.point, obstacleLayer);
				if (blockHit.collider == null)
				{
					StartWindup(hit.collider.transform);
				}
			}
		}

		private void StartWindup(Transform playerTarget)
		{
			if (m_currentRoutine != null) StopCoroutine(m_currentRoutine);
			m_currentRoutine = StartCoroutine(WindupRoutine(playerTarget));
		}

		private IEnumerator WindupRoutine(Transform playerTarget)
		{
			m_state = State.Windup;
			m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);

			// Face towards player at the moment of discovery
			if (playerTarget != null)
			{
				float dirToPlayer = Mathf.Sign(playerTarget.position.x - transform.position.x);
				TurnToFace(dirToPlayer);
			}

			SafeSetBool(ChargeHash, true);

			// Telegraph flash effect
			float elapsed = 0f;
			bool flashState = false;
			float interval = 0.05f;
			while (elapsed < chargeWindupTime)
			{
				if (m_state != State.Windup) yield break;

				flashState = !flashState;
				if (m_spriteRenderer != null)
				{
					m_spriteRenderer.color = flashState ? windupColor : m_originalColor;
				}
				yield return new WaitForSeconds(interval);
				elapsed += interval;
			}

			if (m_spriteRenderer != null) m_spriteRenderer.color = m_originalColor;

			if (m_state == State.Windup)
			{
				StartCharge();
			}
		}

		private void StartCharge()
		{
			m_state = State.Charging;
			m_chargeTimer = 0f;
			if (m_stats != null) m_stats.IsAttacking = true;
		}

		private void HandleCharging()
		{
			m_chargeTimer += Time.fixedDeltaTime;
			m_rb.linearVelocity = new Vector2(m_facingDirection * chargeSpeed, m_rb.linearVelocity.y);

			// Full height BoxCast to detect wall / low ceiling during charge
			Bounds colBounds = m_col != null ? m_col.bounds : new Bounds(transform.position, new Vector3(0.8f, 0.8f, 0f));
			Vector2 boxSize = new Vector2(0.1f, colBounds.size.y * 0.8f);
			Vector2 direction = Vector2.right * m_facingDirection;
			float halfWidth = colBounds.extents.x;

			RaycastHit2D wallHit = Physics2D.BoxCast(colBounds.center, boxSize, 0f, direction, halfWidth + wallCheckDistance, obstacleLayer);

			if (wallHit.collider != null && wallHit.collider != m_col)
			{
				OnHitWall();
				return;
			}

			// Max charge duration timeout: gets tired
			if (m_chargeTimer >= maxChargeDuration)
			{
				OnChargeTimeout();
			}
		}

		private void OnHitWall()
		{
			if (m_state != State.Charging) return;
			if (m_currentRoutine != null) StopCoroutine(m_currentRoutine);
			m_currentRoutine = StartCoroutine(WallImpactRoutine());
		}

		private IEnumerator WallImpactRoutine()
		{
			m_state = State.Recoil;
			if (m_stats != null) m_stats.IsAttacking = false;

			SafeSetBool(ChargeHash, false);
			SafeSetBool(StunnedHash, true);

			if (m_spriteRenderer != null) m_spriteRenderer.color = stunnedColor;

			// Physical recoil impulse
			m_rb.linearVelocity = Vector2.zero;
			m_rb.AddForce(new Vector2(-m_facingDirection * recoilForce.x, recoilForce.y), ForceMode2D.Impulse);

			m_recoilTimer = 0.25f;
			yield return new WaitForSeconds(0.25f);

			m_state = State.Stunned;
			m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);

			yield return new WaitForSeconds(stunDurationSelf);

			EndStun();
		}

		private void OnChargeTimeout()
		{
			if (m_state != State.Charging) return;
			if (m_currentRoutine != null) StopCoroutine(m_currentRoutine);
			m_currentRoutine = StartCoroutine(TiredRoutine());
		}

		private IEnumerator TiredRoutine()
		{
			m_state = State.Stunned;
			if (m_stats != null) m_stats.IsAttacking = false;

			SafeSetBool(ChargeHash, false);
			SafeSetBool(StunnedHash, true);

			m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
			if (m_spriteRenderer != null) m_spriteRenderer.color = stunnedColor;

			yield return new WaitForSeconds(tiredStunDuration);

			EndStun();
		}

		private void EndStun()
		{
			if (m_spriteRenderer != null) m_spriteRenderer.color = m_originalColor;
			SafeSetBool(StunnedHash, false);
			m_cooldownTimer = chargeCooldown;
			m_state = State.Patrol;
		}

		private void OnCollisionEnter2D(Collision2D collision)
		{
			HandleCollisionEvent(collision);
		}

		private void OnCollisionStay2D(Collision2D collision)
		{
			HandleCollisionEvent(collision);
		}

		private void HandleCollisionEvent(Collision2D collision)
		{
			// 1. Obstacle collision during Patrol (low walls, low ceilings, obstacles)
			if (m_state == State.Patrol && !m_isWaiting)
			{
				if (((1 << collision.gameObject.layer) & obstacleLayer) != 0)
				{
					for (int i = 0; i < collision.contactCount; i++)
					{
						ContactPoint2D contact = collision.GetContact(i);
						if (Vector2.Dot(contact.normal, Vector2.right * m_facingDirection) < -0.3f)
						{
							StopAndTurnAround();
							return;
						}
					}
				}
			}

			// 2. Charging collisions
			if (m_state == State.Charging)
			{
				// Player Collision
				if (collision.gameObject.CompareTag("Player") || ((1 << collision.gameObject.layer) & playerLayer) != 0)
				{
					PlayerController player = collision.gameObject.GetComponent<PlayerController>();
					if (player != null)
					{
						Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
						Vector2 pVel = playerRb != null ? playerRb.linearVelocity : Vector2.zero;
						bool isFalling = pVel.y < 0.1f;
						bool isAbove = player.transform.position.y > transform.position.y + 0.3f;

						// If the player is stomping from above, let EnemyStats handle the stomp
						if (isFalling && isAbove && !player.isHurt)
						{
							return;
						}

						// Frontal body hit
						player.TakeDamage(transform.position);
						OnHitWall();
						return;
					}
				}

				// Obstacle / Ground Wall Collision
				if (((1 << collision.gameObject.layer) & obstacleLayer) != 0)
				{
					for (int i = 0; i < collision.contactCount; i++)
					{
						ContactPoint2D contact = collision.GetContact(i);
						if (Vector2.Dot(contact.normal, Vector2.right * m_facingDirection) < -0.3f)
						{
							OnHitWall();
							return;
						}
					}
				}
			}
		}

		public void TurnToFace(float dir)
		{
			if (Mathf.Approximately(dir, 0f)) return;
			m_facingDirection = dir > 0 ? 1f : -1f;

			if (m_spriteRenderer != null)
			{
				m_spriteRenderer.flipX = (m_facingDirection < 0);
			}
			else
			{
				Vector3 s = transform.localScale;
				s.x = Mathf.Abs(s.x) * m_facingDirection;
				transform.localScale = s;
			}
		}

		public void InterruptToStun(float duration)
		{
			if (m_state == State.Dead) return;
			if (m_currentRoutine != null) StopCoroutine(m_currentRoutine);
			m_currentRoutine = StartCoroutine(InterruptedRoutine(duration));
		}

		private IEnumerator InterruptedRoutine(float duration)
		{
			m_state = State.Stunned;
			if (m_stats != null) m_stats.IsAttacking = false;
			SafeSetBool(ChargeHash, false);
			SafeSetBool(StunnedHash, true);

			if (m_spriteRenderer != null) m_spriteRenderer.color = stunnedColor;

			yield return new WaitForSeconds(duration);

			EndStun();
		}

		public void OnDeath()
		{
			m_state = State.Dead;
			if (m_currentRoutine != null) StopCoroutine(m_currentRoutine);
			if (m_spriteRenderer != null) m_spriteRenderer.color = m_originalColor;
			SafeSetBool(ChargeHash, false);
			SafeSetBool(StunnedHash, false);
			this.enabled = false;
		}

		private void OnDrawGizmos()
		{
			// Waypoint Gizmos
			if (patrolPoints != null && patrolPoints.Length > 0)
			{
				Gizmos.color = Color.green;
				for (int i = 0; i < patrolPoints.Length; i++)
				{
					Transform p1 = patrolPoints[i];
					if (p1 != null) Gizmos.DrawSphere(p1.position, 0.2f);
				}
			}

			// Detection BoxCast Gizmo
			Gizmos.color = m_state == State.Charging ? Color.red : (m_state == State.Windup ? Color.magenta : Color.yellow);
			Vector2 origin = (Vector2)transform.position + detectionOffset;
			Vector3 center = origin + Vector2.right * m_facingDirection * (detectionRange / 2f);
			Gizmos.DrawWireCube(center, new Vector3(detectionRange, detectionBoxHeight, 0.1f));

			// Wall Check Gizmo (full box)
			Gizmos.color = Color.cyan;
			Bounds colBounds = m_col != null ? m_col.bounds : new Bounds(transform.position, new Vector3(0.8f, 0.8f, 0f));
			Vector2 boxSize = new Vector2(0.1f, colBounds.size.y * 0.8f);
			float halfWidth = colBounds.extents.x;
			Vector3 checkCenter = colBounds.center + Vector3.right * (m_facingDirection * (halfWidth + wallCheckDistance / 2f));
			Gizmos.DrawWireCube(checkCenter, new Vector3(wallCheckDistance, boxSize.y, 0.1f));

			// Ledge Check Gizmo
			Gizmos.color = Color.blue;
			Vector2 ledgeOrigin = (Vector2)transform.position + Vector2.up * 0.2f + Vector2.right * (m_facingDirection * (halfWidth + ledgeCheckForwardOffset));
			Gizmos.DrawLine(ledgeOrigin, ledgeOrigin + Vector2.down * ledgeCheckDownDistance);
		}
	}
}
