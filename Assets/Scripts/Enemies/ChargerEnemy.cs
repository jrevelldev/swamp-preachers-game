using UnityEngine;
using System.Collections;

namespace SwampPreachers.Enemies
{
	[RequireComponent(typeof(Rigidbody2D))]
	[RequireComponent(typeof(EnemyStats))]
	public class ChargerEnemy : MonoBehaviour
	{
		[Header("Patrol Settings")]
		[SerializeField] private Transform[] patrolPoints;
		[SerializeField] private float patrolSpeed = 2f;
		[SerializeField] private float waitTime = 1f;

		[Header("Charger Settings")]
		[SerializeField] private float detectionRange = 6f;
		[SerializeField] private float verticalDetectionThreshold = 1.5f; // Increased for better detection
		[SerializeField] private float chargeSpeed = 8f;
		[SerializeField] private float chargeWindupTime = 0.5f;
		[SerializeField] private float stunDurationSelf = 2.0f;
		[SerializeField] private LayerMask playerLayer;
		[SerializeField] private LayerMask obstacleLayer;

		private void Reset()
		{
			playerLayer = LayerMask.GetMask("Player");
			obstacleLayer = LayerMask.GetMask("Ground", "Obstacle", "Default");
			if (GetComponent<BoxCollider2D>() == null) gameObject.AddComponent<BoxCollider2D>();
			if (GetComponent<Rigidbody2D>() == null) gameObject.AddComponent<Rigidbody2D>();
			if (GetComponent<EnemyStats>() == null) gameObject.AddComponent<EnemyStats>();
		}

		private enum State
		{
			Patrol,
			Windup,
			Charging,
			Stunned
		}

		private State m_state = State.Patrol;
		private float m_facingDirection = 1f;
		private int m_currentPointIndex = 0;
		private float m_waitTimer = 0f;
		private bool m_isWaiting;

		private Rigidbody2D m_rb;
		private Animator m_anim;
		private SpriteRenderer m_spriteRenderer;
		private EnemyStats m_stats;

		private static readonly int SpeedHash = Animator.StringToHash("Speed");
		private static readonly int ChargeHash = Animator.StringToHash("Charge");
		private static readonly int StunnedHash = Animator.StringToHash("Stunned");

		private void Start()
		{
			m_rb = GetComponent<Rigidbody2D>();
			m_stats = GetComponent<EnemyStats>();
			m_anim = GetComponent<Animator>();
			if(m_anim == null) m_anim = GetComponentInChildren<Animator>();
			m_spriteRenderer = GetComponentInChildren<SpriteRenderer>();

			m_facingDirection = transform.localScale.x > 0 ? 1f : -1f;

			if (patrolPoints != null && patrolPoints.Length > 0)
			{
				m_currentPointIndex = 0;
			}

			// Validate Layers
			if (playerLayer == 0) 
			{
				playerLayer = LayerMask.GetMask("Player");
				if (playerLayer == 0) Debug.LogWarning("ChargerEnemy: Player Layer is empty! Please set it in Inspector.");
			}
			if (obstacleLayer == 0) 
			{
				obstacleLayer = LayerMask.GetMask("Ground", "Default");
			}
		}

		private void FixedUpdate()
		{
			if (m_stats.IsStunned)
			{
				// If stats say we are stunned (e.g. from damage), ensure we respect that overlay
				m_rb.linearVelocity = Vector2.zero;
				return;
			}

			switch (m_state)
			{
				case State.Patrol:
					HandlePatrol();
					CheckForPlayer();
					break;
				case State.Windup:
					m_rb.linearVelocity = Vector2.zero;
					break;
				case State.Charging:
					HandleCharging();
					break;
				case State.Stunned:
					// Handled by coroutine mostly, but ensure no movement
					m_rb.linearVelocity = Vector2.zero;
					break;
			}
			
			if (m_anim != null)
			{
				m_anim.SetFloat(SpeedHash, Mathf.Abs(m_rb.linearVelocity.x));
			}
		}

		private void HandlePatrol()
		{
			if (patrolPoints == null || patrolPoints.Length == 0) return;

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
					TurnToFace(dirX);
					m_rb.linearVelocity = new Vector2(dirX * patrolSpeed, m_rb.linearVelocity.y);
				}
			}
		}

		private void CheckForPlayer()
		{
			// Raycast for player
			Vector2 origin = transform.position;
			Vector2 direction = Vector2.right * m_facingDirection;

			// Visual debug
			Color rayColor = Color.yellow;
			
			RaycastHit2D hit = Physics2D.Raycast(origin, direction, detectionRange, playerLayer);
			if (hit.collider != null)
			{
				rayColor = Color.red;
				// Check vertical alignment
				float yDiff = Mathf.Abs(hit.transform.position.y - transform.position.y);
				if (yDiff < verticalDetectionThreshold)
				{
					Debug.Log($"Charger Detected Player! Y-Diff: {yDiff}");
					StartCoroutine(WindupRoutine());
				}
			}
			Debug.DrawRay(origin, direction * detectionRange, rayColor);
		}

		private void HandleCharging()
		{
			m_rb.linearVelocity = new Vector2(m_facingDirection * chargeSpeed, m_rb.linearVelocity.y);

			float wallCheckDist = 0.6f;
			RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.right * m_facingDirection, wallCheckDist, obstacleLayer);
			
			if (hit.collider != null)
			{
				StartCoroutine(StunSelfRoutine());
			}
		}

		private IEnumerator WindupRoutine()
		{
			m_state = State.Windup;
			if (m_anim != null) m_anim.SetBool(ChargeHash, true); // Use Charge bool for windup/charge? Or trigger?
			// Let's assume Charge bool True = Windup+Charge animation loop
			
			yield return new WaitForSeconds(chargeWindupTime);
			
			m_state = State.Charging;
			m_stats.IsAttacking = true; 
		}

		private IEnumerator StunSelfRoutine()
		{
			m_state = State.Stunned;
			m_stats.IsAttacking = false;
			if (m_anim != null) 
			{
				m_anim.SetBool(ChargeHash, false);
				m_anim.SetBool(StunnedHash, true);
			}
			
			// Small bounce back?
			m_rb.AddForce(new Vector2(-m_facingDirection * 3f, 2f), ForceMode2D.Impulse);

			yield return new WaitForSeconds(stunDurationSelf);
			
			if (m_anim != null) m_anim.SetBool(StunnedHash, false);
			m_state = State.Patrol;
		}

		private void TurnToFace(float dir)
		{
			if (dir == 0) return;
			m_facingDirection = dir;
			if (m_spriteRenderer != null)
			{
				// If 1 (Right), flipX = false? Depends on sprite. Assuming Right-facing sprite is default
				// If sprite faces Right by default:
				m_spriteRenderer.flipX = (dir < 0);
			}
		}

		private void OnCollisionEnter2D(Collision2D collision)
		{
			if (m_state == State.Charging && collision.gameObject.CompareTag("Player"))
			{
				PlayerController player = collision.gameObject.GetComponent<PlayerController>();
				if (player != null)
				{
					// Deal damage
					player.TakeDamage(transform.position);
					// If we hit player, do we stop? Or keep charging? 
					// Let's stop and stun to be fair/classic
					StartCoroutine(StunSelfRoutine());
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
			Gizmos.DrawLine(transform.position, transform.position + (Vector3.right * m_facingDirection * detectionRange));
		}
	}
}
