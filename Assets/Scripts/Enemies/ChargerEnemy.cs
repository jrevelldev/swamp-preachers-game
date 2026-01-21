using UnityEngine;
using System.Collections;

namespace SwampPreachers.Enemies
{
	[RequireComponent(typeof(Rigidbody2D))]
	[RequireComponent(typeof(EnemyStats))]
	public class ChargerEnemy : MonoBehaviour
	{
		[Header("Charger Settings")]
		[SerializeField] private float detectionRange = 5f;
		[SerializeField] private float chargeSpeed = 8f;
		[SerializeField] private float chargeWindupTime = 0.5f;
		[SerializeField] private float stunDurationSelf = 2.0f;
		[SerializeField] private LayerMask playerLayer;
		[SerializeField] private LayerMask obstacleLayer;

		private enum State
		{
			Idle,
			Windup,
			Charging,
			Stunned
		}

		private State m_state = State.Idle;
		private float m_facingDirection = 1f;

		private Rigidbody2D m_rb;
		private Animator m_anim;
		private SpriteRenderer m_spriteRenderer;
		private EnemyStats m_stats;

		private static readonly int ChargeHash = Animator.StringToHash("Charge");

		private void Start()
		{
			m_rb = GetComponent<Rigidbody2D>();
			m_stats = GetComponent<EnemyStats>();
			m_anim = GetComponent<Animator>();
			if(m_anim == null) m_anim = GetComponentInChildren<Animator>();
			m_spriteRenderer = GetComponentInChildren<SpriteRenderer>();

			m_facingDirection = transform.localScale.x > 0 ? 1f : -1f;
		}

		private void FixedUpdate()
		{
			// Check Health from Stats (Optional, stats handles death, but we stop logic)
			// Alternatively check if object is null (destroyed)

			switch (m_state)
			{
				case State.Idle:
					HandleIdle();
					break;
				case State.Windup:
					m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
					break;
				case State.Charging:
					HandleCharging();
					break;
				case State.Stunned:
					m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
					break;
			}
			
			if (m_anim != null)
			{
				m_anim.SetFloat("Speed", Mathf.Abs(m_rb.linearVelocity.x));
			}
		}

		private void HandleIdle()
		{
			m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
			
			Collider2D player = Physics2D.OverlapCircle(transform.position, detectionRange, playerLayer);
			if (player != null)
			{
				if (Mathf.Abs(player.transform.position.y - transform.position.y) < 1.0f)
				{
					float dir = Mathf.Sign(player.transform.position.x - transform.position.x);
					TurnToFace(dir);
					StartCoroutine(WindupRoutine());
				}
			}
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
			yield return new WaitForSeconds(chargeWindupTime);
			
			m_state = State.Charging;
			m_stats.IsAttacking = true; // Charging is attacking
			if (m_anim != null) m_anim.SetBool(ChargeHash, true);
		}

		private IEnumerator StunSelfRoutine()
		{
			m_state = State.Stunned;
			m_stats.IsAttacking = false;
			if (m_anim != null) m_anim.SetBool(ChargeHash, false);
			
			// Use Stats ApplyKnockback to trigger internal stun routine as well?
			// Or just set visual
			// Let's use stats ApplyKnockback with zero force to trigger stats stun state
			m_stats.ApplyKnockback(Vector2.zero);

			yield return new WaitForSeconds(stunDurationSelf);
			
			m_state = State.Idle;
		}

		private void TurnToFace(float dir)
		{
			m_facingDirection = dir;
			if (m_spriteRenderer != null)
			{
				m_spriteRenderer.flipX = (dir < 0);
			}
		}

		private void OnTriggerEnter2D(Collider2D collision)
		{
			// Stats handles normal body damage
			// We handle charge damage
			if (m_state == State.Charging && collision.CompareTag("Player"))
			{
				PlayerController player = collision.GetComponent<PlayerController>();
				if (player != null)
				{
					player.TakeDamage(transform.position);
					player.Bounce();
				}
			}
		}

		private void OnDrawGizmos()
		{
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(transform.position, detectionRange);
		}
	}
}
