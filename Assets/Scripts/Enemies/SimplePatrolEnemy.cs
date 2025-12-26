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
		[SerializeField] private int maxHealth = 1;
		[SerializeField] private bool canBeStomped = true;

		[Header("UI")]
		[SerializeField] private bool showHealthBar = true;
		[SerializeField] private Vector2 healthBarOffset = new Vector2(0f, 0.8f);
		[SerializeField] private Vector2 healthBarSize = new Vector2(1f, 0.15f);

		private int currentHealth;
		private Transform m_healthBarRoot;
		private Transform m_healthBarFill;
		
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
		private float m_stunTimer;
		private Rigidbody2D m_rb;
		private SpriteRenderer m_spriteRenderer;

		private void Start()
		{
			m_rb = GetComponent<Rigidbody2D>();
			m_spriteRenderer = GetComponentInChildren<SpriteRenderer>();
			currentHealth = maxHealth;

			if (showHealthBar)
				CreateHealthBar();
			
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

			if (m_stunTimer > 0f)
			{
				m_stunTimer -= Time.fixedDeltaTime;
				// Do not process movement while stunned
				return;
			}

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
					// Check for stomp
					// Conditions: CanBeStomped + Player is Falling + Player is Roughly Above + Player NOT Hurt + Player NOT Grounded
					bool isFalling = player.GetComponent<Rigidbody2D>().linearVelocity.y < 0.1f; 
					bool isAbove = player.transform.position.y > transform.position.y + 0.3f; 

					// Critical Fix: Must NOT be grounded. Walking into enemy on ground should hurt player, not stomp.
					if (canBeStomped && isFalling && isAbove && !player.isHurt && !player.isGrounded)
					{
						TakeDamage(1); 
						player.Bounce();
					}
					else
					{
						player.TakeDamage(transform.position);
					}
				}
			}
		}

		public void TakeDamage(int damage)
		{
			currentHealth -= damage;
			
			if (showHealthBar)
				UpdateHealthBar();

			if (m_spriteRenderer != null)
				StartCoroutine(FlashRoutine());

			if (currentHealth <= 0)
			{
				Die();
			}
		}

		public void ApplyKnockback(Vector2 force)
		{
			m_stunTimer = 0.2f; // Stun for 0.2s
			m_rb.linearVelocity = Vector2.zero;
			m_rb.AddForce(force, ForceMode2D.Impulse);
		}

		private System.Collections.IEnumerator FlashRoutine()
		{
			Color original = m_spriteRenderer.color;
			m_spriteRenderer.color = Color.red;
			yield return new WaitForSeconds(0.1f);
			m_spriteRenderer.color = original;
		}

		private void Die()
		{
			// Disable physics and collision immediately so it feels dead/safe
			if (m_rb != null) m_rb.simulated = false;
			Collider2D col = GetComponent<Collider2D>();
			if (col != null) col.enabled = false;
			this.enabled = false; // Stop moving

			// Delay destroy slightly so the Red Flash can render for a frame or two
			Destroy(gameObject, 0.15f);
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

		private void CreateHealthBar()
		{
			// Create Root
			GameObject root = new GameObject("HealthBar");
			root.transform.SetParent(transform);
			root.transform.localPosition = healthBarOffset;
			m_healthBarRoot = root.transform;

			// Generate Texture
			Texture2D tex = new Texture2D(1, 1);
			tex.filterMode = FilterMode.Point; // Crisp edges
			tex.SetPixel(0, 0, Color.white);
			tex.Apply();
			
			// Critical Fix: Set PPU to 1 so 1 pixel = 1 Unity Unit. 
			// Otherwise default is 100 PPU, making the bar 100x smaller than expected.
			Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

			// Background (Red)
			GameObject bg = new GameObject("Background");
			bg.transform.SetParent(root.transform);
			bg.transform.localPosition = Vector3.zero;
			bg.transform.localScale = new Vector3(healthBarSize.x, healthBarSize.y, 1f);
			SpriteRenderer bgSr = bg.AddComponent<SpriteRenderer>();
			bgSr.sprite = sprite;
			bgSr.color = Color.red;
			bgSr.sortingOrder = 100; // Ensure visible (high sorting order)

			// Fill (Green)
			GameObject fill = new GameObject("Fill");
			fill.transform.SetParent(root.transform);
			
			// Create Left-Pivoted Sprite with PPU 1
			Sprite leftPivotSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);

			fill.transform.localPosition = new Vector3(-healthBarSize.x / 2f, 0f, 0f); // Start at left edge
			fill.transform.localScale = new Vector3(healthBarSize.x, healthBarSize.y, 1f);
			
			m_healthBarFill = fill.transform;
			SpriteRenderer fillSr = fill.AddComponent<SpriteRenderer>();
			fillSr.sprite = leftPivotSprite;
			fillSr.color = Color.green;
			fillSr.sortingOrder = 101; // Above background
		}

		private void UpdateHealthBar()
		{
			if (m_healthBarFill != null)
			{
				float pct = Mathf.Clamp01((float)currentHealth / maxHealth);
				// Since fill is left-pivoted, we just scale X.
				// Initial scale X is healthBarSize.x. New scale is healthBarSize.x * pct.
				Vector3 s = m_healthBarFill.localScale;
				s.x = healthBarSize.x * pct;
				m_healthBarFill.localScale = s;
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
