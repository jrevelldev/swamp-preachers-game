using UnityEngine;
using SwampPreachers;

namespace SwampPreachers.Enemies
{
		[RequireComponent(typeof(Rigidbody2D))]
	public class SimplePatrolEnemy : MonoBehaviour
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
		private Rigidbody2D m_rb;

		private void Start()
		{
			m_rb = GetComponent<Rigidbody2D>();
			
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
			if (patrolPoints == null || patrolPoints.Length == 0) return;

			if (m_isWaiting)
			{
				m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y); // Stop horizontal, keep vertical (gravity)
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
				// Move towards target
				
				if (movementType == MovementType.Flying)
				{
					// --- FLYING LOGIC ---
					float dist = Vector2.Distance(transform.position, target.position);
					if (dist < 0.2f)
					{
						// Reached point
						m_isWaiting = true;
						m_waitTimer = waitTime;
						m_rb.linearVelocity = Vector2.zero;
					}
					else
					{
						Vector2 direction = (target.position - transform.position).normalized;
						
						if (pathType == PathType.SineWave)
						{
							// Calculate perpendicular vector for wave motion (e.g., (-y, x))
							Vector2 perp = new Vector2(-direction.y, direction.x);
							// Add sine wave to velocity
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
					// --- GROUND LOGIC ---
					// Use X distance only for ground enemies
					float dist = Mathf.Abs(transform.position.x - target.position.x);
					
					if (dist < 0.2f)
					{
						// Reached point
						m_isWaiting = true;
						m_waitTimer = waitTime;
						m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
					}
					else
					{
						// Determine direction based on X difference
						float dirX = Mathf.Sign(target.position.x - transform.position.x);
						m_rb.linearVelocity = new Vector2(dirX * speed, m_rb.linearVelocity.y);
					}
				}
			}
		}

		private void OnCollisionEnter2D(Collision2D collision)
		{
			if (collision.gameObject.CompareTag("Player"))
			{
				PlayerController player = collision.gameObject.GetComponent<PlayerController>();
				if (player != null)
				{
					player.TakeDamage(transform.position);
				}
			}
		}

		private void OnTriggerEnter2D(Collider2D collision)
		{
			if (collision.CompareTag("Player"))
			{
				PlayerController player = collision.GetComponent<PlayerController>();
				if (player != null)
				{
					player.TakeDamage(transform.position);
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
				Transform p2 = patrolPoints[(i + 1) % patrolPoints.Length]; // Connect back to start?
				// User might not want a loop if they just put 2 points. 
				// Logic uses (i+1)%length, so it IS a loop.
				
				if (p1 != null && p2 != null)
				{
					Gizmos.DrawLine(p1.position, p2.position);
					Gizmos.DrawSphere(p1.position, 0.2f);
				}
			}
		}
	}
}
