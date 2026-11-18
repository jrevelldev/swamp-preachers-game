using System.Collections.Generic;
using UnityEngine;
using SwampPreachers;

namespace SwampPreachers.Enemies
{
	[RequireComponent(typeof(Rigidbody2D))]
	[RequireComponent(typeof(EnemyStats))]
	public class PatrolEnemy : MonoBehaviour
	{
		[Header("Patrol Settings")]
		[Tooltip("Optional patrol waypoints. If empty, the enemy will patrol autonomously using edge and wall detection.")]
		[SerializeField] private Transform[] patrolPoints;
		[SerializeField] private float speed = 2f;
		[SerializeField] private float waitTime = 1f;

		private enum MovementType
		{
			Ground,
			Flying
		}

		private enum PathType
		{
			Linear,
			SineWave
		}

		[SerializeField] private MovementType movementType = MovementType.Ground;
		[SerializeField] private PathType pathType = PathType.Linear;
		[SerializeField] private float sineFrequency = 2f;
		[SerializeField] private float sineAmplitude = 2f;

		[Header("Autonomous Ground Patrol (When No Points Set)")]
		[SerializeField] private LayerMask groundLayer;
		[SerializeField] private float wallCheckDistance = 0.4f;
		[SerializeField] private float ledgeCheckForwardOffset = 0.5f;
		[SerializeField] private float ledgeCheckDownDistance = 1.0f;

		private List<Transform> m_validPatrolPoints = new List<Transform>();
		private int m_currentPointIndex;
		private float m_waitTimer;
		private bool m_isWaiting;
		private float m_facingDirection = 1f;

		[Header("Attack Settings")]
		[SerializeField] private bool isAggressive = false;
		[SerializeField] private float attackRange = 1.0f;
		[SerializeField] private float attackCooldown = 2.0f;
		[SerializeField] private LayerMask playerLayer;

		private float m_attackTimer;
		private bool m_isAttacking;
		
		private Rigidbody2D m_rb;
		private Animator m_anim;
		private SpriteRenderer m_spriteRenderer;
		private EnemyStats m_stats;

		// Hash IDs
		private static readonly int SpeedHash = Animator.StringToHash("Speed");
		private static readonly int AttackHash = Animator.StringToHash("Attack");

		private bool HasParameter(int paramHash)
		{
			if (m_anim == null || m_anim.runtimeAnimatorController == null) return false;
			foreach (var p in m_anim.parameters)
			{
				if (p.nameHash == paramHash) return true;
			}
			return false;
		}

		private void SafeSetFloat(int hash, float value)
		{
			if (HasParameter(hash))
			{
				m_anim.SetFloat(hash, value);
			}
		}

		private void SafeSetTrigger(int hash)
		{
			if (HasParameter(hash))
			{
				m_anim.SetTrigger(hash);
			}
		}

		private void Start()
		{
			m_rb = GetComponent<Rigidbody2D>();
			m_stats = GetComponent<EnemyStats>();
			m_anim = GetComponent<Animator>();
			if (m_anim == null) m_anim = GetComponentInChildren<Animator>();
			m_spriteRenderer = GetComponentInChildren<SpriteRenderer>();

			if (movementType == MovementType.Flying)
			{
				m_rb.gravityScale = 0f;
			}

			// Validate and filter patrol points (remove null entries)
			m_validPatrolPoints.Clear();
			if (patrolPoints != null)
			{
				foreach (var p in patrolPoints)
				{
					if (p != null) m_validPatrolPoints.Add(p);
				}
			}

			if (m_validPatrolPoints.Count > 0)
			{
				m_currentPointIndex = 0;
			}

			// Validate layers
			if (playerLayer == 0)
			{
				playerLayer = LayerMask.GetMask("Player");
			}
			if (groundLayer == 0)
			{
				groundLayer = LayerMask.GetMask("Ground", "Default");
			}

			// Initial facing direction from scale or sprite
			if (transform.localScale.x < 0) m_facingDirection = -1f;
			else m_facingDirection = 1f;
		}

		private void FixedUpdate()
		{
			if (m_stats != null && m_stats.IsDead)
			{
				return;
			}

			// Check Stun from Stats
			if (m_stats != null && m_stats.IsStunned)
			{
				// Let physics knockback resolve naturally without forcing velocity to zero
				return;
			}

			// Attack Timer
			if (m_attackTimer > 0f) m_attackTimer -= Time.fixedDeltaTime;

			if (m_isAttacking)
			{
				m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
				return;
			}

			if (isAggressive)
			{
				CheckForAttack();
				if (m_isAttacking) return;
			}

			PerformPatrol();
		}

		private void CheckForAttack()
		{
			Collider2D playerCol = Physics2D.OverlapCircle(transform.position, attackRange, playerLayer);
			if (playerCol != null)
			{
				if (m_attackTimer <= 0f)
				{
					StartCoroutine(AttackRoutine());
				}
			}
		}

		private System.Collections.IEnumerator AttackRoutine()
		{
			m_isAttacking = true;
			if (m_stats != null) m_stats.IsAttacking = true;
			
			m_rb.linearVelocity = Vector2.zero;
			
			SafeSetTrigger(AttackHash);
			
			yield return new WaitForSeconds(0.4f); // Windup
			
			// Deal damage
			Collider2D playerCol = Physics2D.OverlapCircle(transform.position, attackRange, playerLayer);
			if (playerCol != null)
			{
				PlayerController player = playerCol.GetComponent<PlayerController>();
				if (player != null)
				{
					player.TakeDamage(transform.position);
				}
			}

			yield return new WaitForSeconds(0.4f); // Recovery
			
			m_isAttacking = false;
			if (m_stats != null) m_stats.IsAttacking = false;
			m_attackTimer = attackCooldown;
		}

		private void PerformPatrol()
		{
			SafeSetFloat(SpeedHash, Mathf.Abs(m_rb.linearVelocity.x));

			// If we have explicit patrol points, follow them
			if (m_validPatrolPoints != null && m_validPatrolPoints.Count >= 2)
			{
				PerformWaypointPatrol();
			}
			else if (movementType == MovementType.Ground)
			{
				// Autonomous Ground Patrol (Ledge & Wall detection)
				PerformAutonomousPatrol();
			}
		}

		private void PerformWaypointPatrol()
		{
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
			if (target == null)
			{
				// Skip invalid point
				m_currentPointIndex = (m_currentPointIndex + 1) % m_validPatrolPoints.Count;
				return;
			}

			bool isFlying = movementType == MovementType.Flying;
			float dirX = Mathf.Sign(target.position.x - transform.position.x);
			SetFacingDirection(dirX);

			if (isFlying)
			{
				float dist = Vector2.Distance(transform.position, target.position);
				if (dist < 0.25f)
				{
					m_isWaiting = true;
					m_waitTimer = waitTime;
					m_rb.linearVelocity = Vector2.zero;
				}
				else
				{
					Vector2 direction = (target.position - transform.position).normalized;
					if (pathType == PathType.SineWave)
					{
						Vector2 perp = new Vector2(-direction.y, direction.x);
						float wave = Mathf.Sin(Time.time * sineFrequency) * sineAmplitude;
						m_rb.linearVelocity = (direction * speed) + (perp * wave);
					}
					else
					{
						m_rb.linearVelocity = direction * speed;
					}
				}
			}
			else
			{
				float dist = Mathf.Abs(transform.position.x - target.position.x);
				if (dist < 0.25f)
				{
					m_isWaiting = true;
					m_waitTimer = waitTime;
					m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
				}
				else
				{
					m_rb.linearVelocity = new Vector2(dirX * speed, m_rb.linearVelocity.y);
				}
			}
		}

		private void PerformAutonomousPatrol()
		{
			if (m_isWaiting)
			{
				m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
				m_waitTimer -= Time.fixedDeltaTime;
				if (m_waitTimer <= 0f)
				{
					m_isWaiting = false;
					SetFacingDirection(-m_facingDirection);
				}
				return;
			}

			Vector2 origin = (Vector2)transform.position + Vector2.up * 0.2f;
			Vector2 forward = Vector2.right * m_facingDirection;

			// 1. Wall check (cast forward)
			RaycastHit2D wallHit = Physics2D.Raycast(origin, forward, wallCheckDistance, groundLayer);
			bool hitWall = wallHit.collider != null;

			// 2. Ledge check (cast downward ahead of the enemy)
			Vector2 ledgeCheckOrigin = origin + (forward * ledgeCheckForwardOffset);
			RaycastHit2D groundHit = Physics2D.Raycast(ledgeCheckOrigin, Vector2.down, ledgeCheckDownDistance, groundLayer);
			bool reachedLedge = groundHit.collider == null;

			if (hitWall || reachedLedge)
			{
				m_isWaiting = true;
				m_waitTimer = waitTime;
				m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
				return;
			}

			m_rb.linearVelocity = new Vector2(m_facingDirection * speed, m_rb.linearVelocity.y);
		}

		private void SetFacingDirection(float dir)
		{
			if (Mathf.Approximately(dir, 0f)) return;
			m_facingDirection = dir > 0 ? 1f : -1f;

			if (m_spriteRenderer != null)
			{
				m_spriteRenderer.flipX = m_facingDirection < 0;
			}
			else
			{
				Vector3 s = transform.localScale;
				s.x = Mathf.Abs(s.x) * m_facingDirection;
				transform.localScale = s;
			}
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
			else if (movementType == MovementType.Ground)
			{
				// Autonomous patrol debug gizmos
				Gizmos.color = Color.blue;
				Vector2 origin = (Vector2)transform.position + Vector2.up * 0.2f;
				Vector2 forward = Vector2.right * m_facingDirection;
				Gizmos.DrawLine(origin, origin + forward * wallCheckDistance);

				Gizmos.color = Color.yellow;
				Vector2 ledgeCheckOrigin = origin + (forward * ledgeCheckForwardOffset);
				Gizmos.DrawLine(ledgeCheckOrigin, ledgeCheckOrigin + Vector2.down * ledgeCheckDownDistance);
			}
			
			if (isAggressive)
			{
				Gizmos.color = Color.red;
				Gizmos.DrawWireSphere(transform.position, attackRange);
			}
		}
	}
}
