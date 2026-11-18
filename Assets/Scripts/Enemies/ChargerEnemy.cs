using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SwampPreachers.Enemies
{
	[RequireComponent(typeof(Rigidbody2D))]
	[RequireComponent(typeof(EnemyStats))]
	public class ChargerEnemy : MonoBehaviour
	{
		[Header("Patrol Settings")]
		[Tooltip("Optional waypoints. If empty, uses autonomous ground patrol.")]
		[SerializeField] private Transform[] patrolPoints;
		[SerializeField] private float patrolSpeed = 2f;
		[SerializeField] private float waitTime = 1f;

		[Header("Charger Detection")]
		[SerializeField] private float detectionRange = 7f;
		[SerializeField] private float detectionBoxHeight = 1.8f;
		[SerializeField] private Vector2 detectionOffset = new Vector2(0f, 0.5f);
		[SerializeField] private LayerMask playerLayer;
		[SerializeField] private LayerMask obstacleLayer;

		[Header("Charge Behavior")]
		[SerializeField] private float chargeSpeed = 9f;
		[SerializeField] private float chargeWindupTime = 0.5f;
		[SerializeField] private float maxChargeDuration = 2.0f;
		[SerializeField] private float stunDurationSelf = 1.5f;
		[SerializeField] private float wallCheckDistance = 0.5f;

		private enum State
		{
			Patrol,
			Windup,
			Charging,
			Stunned
		}

		private State m_state = State.Patrol;
		private float m_facingDirection = 1f;
		private List<Transform> m_validPatrolPoints = new List<Transform>();
		private int m_currentPointIndex = 0;
		private float m_waitTimer = 0f;
		private bool m_isWaiting;
		private float m_chargeTimer = 0f;

		private Rigidbody2D m_rb;
		private Animator m_anim;
		private SpriteRenderer m_spriteRenderer;
		private EnemyStats m_stats;

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

		private void Start()
		{
			m_rb = GetComponent<Rigidbody2D>();
			m_stats = GetComponent<EnemyStats>();
			m_anim = GetComponent<Animator>();
			if (m_anim == null) m_anim = GetComponentInChildren<Animator>();
			m_spriteRenderer = GetComponentInChildren<SpriteRenderer>();

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

			m_facingDirection = transform.localScale.x < 0 ? -1f : 1f;
		}

		private void FixedUpdate()
		{
			if (m_stats != null && m_stats.IsStunned && m_state != State.Stunned)
			{
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
					m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
					break;
				case State.Charging:
					HandleCharging();
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
					}
					return;
				}

				Vector2 origin = (Vector2)transform.position + Vector2.up * 0.3f;
				Vector2 forward = Vector2.right * m_facingDirection;

				RaycastHit2D wallHit = Physics2D.Raycast(origin, forward, 0.5f, obstacleLayer);
				Vector2 ledgeOrigin = origin + forward * 0.5f;
				RaycastHit2D groundHit = Physics2D.Raycast(ledgeOrigin, Vector2.down, 1.0f, obstacleLayer);

				if (wallHit.collider != null || groundHit.collider == null)
				{
					m_isWaiting = true;
					m_waitTimer = waitTime;
					m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);
					return;
				}

				m_rb.linearVelocity = new Vector2(m_facingDirection * patrolSpeed, m_rb.linearVelocity.y);
			}
		}

		private void CheckForPlayer()
		{
			Vector2 origin = (Vector2)transform.position + detectionOffset;
			Vector2 boxSize = new Vector2(0.2f, detectionBoxHeight);
			Vector2 direction = Vector2.right * m_facingDirection;

			// BoxCast forward covering full player height
			RaycastHit2D hit = Physics2D.BoxCast(origin, boxSize, 0f, direction, detectionRange, playerLayer);
			
			if (hit.collider != null)
			{
				// Check line of sight (ensure no obstacle blocks vision)
				RaycastHit2D blockHit = Physics2D.Linecast(origin, hit.point, obstacleLayer);
				if (blockHit.collider == null)
				{
					StartCoroutine(WindupRoutine());
				}
			}
		}

		private void HandleCharging()
		{
			m_chargeTimer += Time.fixedDeltaTime;
			m_rb.linearVelocity = new Vector2(m_facingDirection * chargeSpeed, m_rb.linearVelocity.y);

			// Wall check during charge (cast slightly above feet)
			Vector2 wallCheckOrigin = (Vector2)transform.position + Vector2.up * 0.4f;
			RaycastHit2D hit = Physics2D.Raycast(wallCheckOrigin, Vector2.right * m_facingDirection, wallCheckDistance, obstacleLayer);
			
			if (hit.collider != null)
			{
				StartCoroutine(StunSelfRoutine(hitWall: true));
				return;
			}

			// Max charge duration timeout
			if (m_chargeTimer >= maxChargeDuration)
			{
				StartCoroutine(StunSelfRoutine(hitWall: false));
			}
		}

		private IEnumerator WindupRoutine()
		{
			m_state = State.Windup;
			m_rb.linearVelocity = new Vector2(0f, m_rb.linearVelocity.y);

			SafeSetBool(ChargeHash, true);
			
			yield return new WaitForSeconds(chargeWindupTime);
			
			m_chargeTimer = 0f;
			m_state = State.Charging;
			if (m_stats != null) m_stats.IsAttacking = true; 
		}

		private IEnumerator StunSelfRoutine(bool hitWall)
		{
			m_state = State.Stunned;
			if (m_stats != null) m_stats.IsAttacking = false;

			SafeSetBool(ChargeHash, false);
			SafeSetBool(StunnedHash, true);
			
			if (hitWall)
			{
				// Recoil impulse
				m_rb.linearVelocity = Vector2.zero;
				m_rb.AddForce(new Vector2(-m_facingDirection * 4f, 3f), ForceMode2D.Impulse);
			}
			else
			{
				m_rb.linearVelocity = Vector2.zero;
			}

			yield return new WaitForSeconds(stunDurationSelf);
			
			SafeSetBool(StunnedHash, false);
			m_state = State.Patrol;
		}

		private void TurnToFace(float dir)
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

		private void OnCollisionEnter2D(Collision2D collision)
		{
			if (m_state == State.Charging && collision.gameObject.CompareTag("Player"))
			{
				PlayerController player = collision.gameObject.GetComponent<PlayerController>();
				if (player != null)
				{
					player.TakeDamage(transform.position);
					StartCoroutine(StunSelfRoutine(hitWall: true));
				}
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

			// Detection BoxCast Gizmo
			Gizmos.color = m_state == State.Charging ? Color.red : Color.yellow;
			Vector2 origin = (Vector2)transform.position + detectionOffset;
			Vector2 target = origin + Vector2.right * m_facingDirection * detectionRange;
			Gizmos.DrawWireCube(origin + Vector2.right * m_facingDirection * (detectionRange / 2f), new Vector3(detectionRange, detectionBoxHeight, 0.1f));

			// Wall Check Gizmo
			Gizmos.color = Color.cyan;
			Vector2 wallCheckOrigin = (Vector2)transform.position + Vector2.up * 0.4f;
			Gizmos.DrawLine(wallCheckOrigin, wallCheckOrigin + Vector2.right * m_facingDirection * wallCheckDistance);
		}
	}
}
