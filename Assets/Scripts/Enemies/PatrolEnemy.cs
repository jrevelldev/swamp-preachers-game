using UnityEngine;
using SwampPreachers;

namespace SwampPreachers.Enemies
{
	[RequireComponent(typeof(Rigidbody2D))]
	[RequireComponent(typeof(EnemyStats))]
	public class PatrolEnemy : MonoBehaviour
	{
		[Header("Patrol Settings")]
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

		private int m_currentPointIndex;
		private float m_waitTimer;
		private bool m_isWaiting;

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
		private EnemyStats m_stats; // Reference to Stats

		// Hash IDs
		private static readonly int SpeedHash = Animator.StringToHash("Speed");
		private static readonly int AttackHash = Animator.StringToHash("Attack");

		private void Start()
		{
			m_rb = GetComponent<Rigidbody2D>();
			m_stats = GetComponent<EnemyStats>();
			m_anim = GetComponent<Animator>();
			if(m_anim == null) m_anim = GetComponentInChildren<Animator>();
			m_spriteRenderer = GetComponentInChildren<SpriteRenderer>();

			if (movementType == MovementType.Flying)
			{
				m_rb.gravityScale = 0f;
			}

			if (patrolPoints != null && patrolPoints.Length > 0)
			{
				m_currentPointIndex = 0;
			}
		}

		private void FixedUpdate()
		{
			// Check Stun from Stats
			if (m_stats.IsStunned)
			{
				m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
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
			m_stats.IsAttacking = true; // Tell Stats we are attacking
			
			m_rb.linearVelocity = Vector2.zero;
			
			if (m_anim != null) m_anim.SetTrigger(AttackHash);
			
			yield return new WaitForSeconds(0.5f); // Windup
			
			// Deal damage logic (Specific to this attack)
			Collider2D playerCol = Physics2D.OverlapCircle(transform.position, attackRange, playerLayer);
			if (playerCol != null)
			{
				PlayerController player = playerCol.GetComponent<PlayerController>();
				if (player != null)
				{
					player.TakeDamage(transform.position);
				}
			}

			yield return new WaitForSeconds(0.5f); // Recovery
			
			m_isAttacking = false;
			m_stats.IsAttacking = false;
			m_attackTimer = attackCooldown;
		}

		private void PerformPatrol()
		{
			if (patrolPoints == null || patrolPoints.Length == 0) return;

			if (m_anim != null) m_anim.SetFloat(SpeedHash, Mathf.Abs(m_rb.linearVelocity.x));

			if (m_isWaiting)
			{
				m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
				m_waitTimer -= Time.deltaTime;
				if (m_waitTimer <= 0f)
				{
					m_isWaiting = false;
					m_currentPointIndex = (m_currentPointIndex + 1) % patrolPoints.Length;
				}
				return;
			}

			Transform target = patrolPoints[m_currentPointIndex];
			if (target != null)
			{
				bool isFlying = movementType == MovementType.Flying;
				
				if (target.position.x > transform.position.x) m_spriteRenderer.flipX = true;
				else if (target.position.x < transform.position.x) m_spriteRenderer.flipX = false;

				if (isFlying)
				{
					float dist = Vector2.Distance(transform.position, target.position);
					if (dist < 0.2f)
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
					if (dist < 0.2f)
					{
						m_isWaiting = true;
						m_waitTimer = waitTime;
						m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
					}
					else
					{
						float dirX = Mathf.Sign(target.position.x - transform.position.x);
						m_rb.linearVelocity = new Vector2(dirX * speed, m_rb.linearVelocity.y);
					}
				}
			}
		}

		private void OnDrawGizmos()
		{
			if (patrolPoints == null || patrolPoints.Length < 2) return;

			Gizmos.color = Color.green;
			for (int i = 0; i < patrolPoints.Length; i++)
			{
				Transform p1 = patrolPoints[i];
				if (p1 != null) Gizmos.DrawSphere(p1.position, 0.2f);
			}
			
			Gizmos.color = Color.red;
			Gizmos.DrawWireSphere(transform.position, attackRange);
		}
	}
}
