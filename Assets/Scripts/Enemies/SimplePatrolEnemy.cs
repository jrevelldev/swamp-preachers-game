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

		private int m_currentPointIndex;
		private float m_waitTimer;
		private bool m_isWaiting;
		private Rigidbody2D m_rb;

		private void Start()
		{
			m_rb = GetComponent<Rigidbody2D>();
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
				// We care about X distance primarily if we are a ground enemy walking?
				// But let's support basic 2D flight/walk. 
				// Problem: If Point is slightly higher than ground, RB logic might struggle if not "flying".
				// Assuming "floating" comment implies falling is desired, so it's a Walker.
				// Walker logic: Move X towards Target.X. preserve Y velocity.
				
				float dist = Vector2.Distance(transform.position, target.position);
				// If we want to strictly follow path in air (platforms), we use MovePosition or Vel.
				// But user asked for gravity. So likely walking on ground.
				
				if (dist < 0.2f)
				{
					// Reached point
					m_isWaiting = true;
					m_waitTimer = waitTime;
					m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
				}
				else
				{
					Vector2 direction = (target.position - transform.position).normalized;
					// Apply only X component for walking, preserve Y (gravity)
					// Verify if we want to jump/fly? Assuming Walking for now given "gravity" request.
					m_rb.linearVelocity = new Vector2(direction.x * speed, m_rb.linearVelocity.y);
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
