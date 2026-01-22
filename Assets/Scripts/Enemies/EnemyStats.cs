using UnityEngine;
using System.Collections;

namespace SwampPreachers.Enemies
{
	[RequireComponent(typeof(Rigidbody2D))]
	public class EnemyStats : MonoBehaviour
	{
		public enum DamageSource
		{
			Attack,
			Stomp,
			Hazard,
			Other
		}

		[Header("Stats")]
		[SerializeField] private int maxHealth = 1;
		[SerializeField] private bool canBeStomped = true;
		[SerializeField] private bool canBeAttacked = true;
		[SerializeField] private bool damageOnResist = false; // "Spikes" behavior
		[SerializeField] private int contactDamage = 1; // Damage dealt to player on touch

		// Public Accessors for Interaction Logic
		public bool CanBeAttacked => canBeAttacked;
		public bool DamageOnResist => damageOnResist;
		public int ContactDamage => contactDamage;

		[Header("UI")]
		[SerializeField] private bool showHealthBar = true;
		[SerializeField] private Vector2 healthBarOffset = new Vector2(0f, 0.8f);
		[SerializeField] private Vector2 healthBarSize = new Vector2(1f, 0.15f);

		[Header("Name Label")]
		[SerializeField] private bool showNameLabel = false;
		[SerializeField] private string enemyName = "Enemy";
		[SerializeField] private Vector2 nameLabelOffset = new Vector2(0f, 1.2f);
		[SerializeField] private int nameFontSize = 24;
		[SerializeField] private float nameScale = 0.1f;
		[SerializeField] private Color nameColor = Color.white;
		// Public properties for other scripts
		public bool IsStunned { get; private set; }
		public bool IsAttacking { get; set; } // Movement scripts can set this to prevent contact damage if needed

		private int currentHealth;
		private Rigidbody2D m_rb;
		private Animator m_anim;
		private SpriteRenderer m_spriteRenderer;
		
		// UI References
		private Transform m_healthBarRoot;
		private Transform m_healthBarFill;

		// Hash IDs needed by Stats
		private static readonly int HitHash = Animator.StringToHash("Hit");
		private static readonly int DieHash = Animator.StringToHash("Die");
		private static readonly int IsStunnedHash = Animator.StringToHash("IsStunned");

		private void Start()
		{
			m_rb = GetComponent<Rigidbody2D>();
			m_anim = GetComponent<Animator>();
			if(m_anim == null) m_anim = GetComponentInChildren<Animator>();
			
			m_spriteRenderer = GetComponentInChildren<SpriteRenderer>();
			
			currentHealth = maxHealth;

			if (showHealthBar)
				CreateHealthBar();
			
			if (showNameLabel)
				CreateNameLabel();
		}

		private void CreateNameLabel()
		{
			GameObject labelObj = new GameObject("NameLabel");
			labelObj.transform.SetParent(transform);
			labelObj.transform.localPosition = nameLabelOffset;
			// Scale down the object so high font size looks crisp
			labelObj.transform.localScale = new Vector3(nameScale, nameScale, 1f);

			TextMesh textMesh = labelObj.AddComponent<TextMesh>();
			textMesh.text = enemyName;
			textMesh.characterSize = 1f; // control size via transform scale + font size
			textMesh.fontSize = nameFontSize;
			textMesh.anchor = TextAnchor.MiddleCenter;
			textMesh.alignment = TextAlignment.Center;
			textMesh.color = nameColor;

			// Ensure it renders on top of sprites
			MeshRenderer mr = labelObj.GetComponent<MeshRenderer>();
			if (mr != null)
			{
				mr.sortingLayerName = "UI"; // Or "Foreground" or default, assuming standard layers
				mr.sortingOrder = 1000; // High order to sit above sprites
			}
		}

		private void FixedUpdate()
		{
			// Sync Stun state with Animator if possible, or just trust our internal boolean
			if (m_anim != null)
			{
				m_anim.SetBool(IsStunnedHash, IsStunned);
			}
		}

		public void TakeDamage(int damage, DamageSource source = DamageSource.Other)
		{
			if (currentHealth <= 0) return;

			// Check Vulnerabilities
			// If vulnerable, take health. If not, just play effects (flash/anim).
			bool isVulnerable = true;
			if (source == DamageSource.Stomp && !canBeStomped) isVulnerable = false;
			if (source == DamageSource.Attack && !canBeAttacked) isVulnerable = false;

			if (isVulnerable)
			{
				currentHealth -= damage;
				if (m_spriteRenderer != null) StartCoroutine(FlashRoutine());
			}
			
			// Trigger Hit Anim (Always play reaction)
			if (m_anim != null) m_anim.SetTrigger(HitHash);

			if (showHealthBar) UpdateHealthBar();

			if (currentHealth <= 0)
			{
				Die();
			}
		}

		public void ApplyKnockback(Vector2 force)
		{
			m_rb.linearVelocity = Vector2.zero;
			m_rb.AddForce(force, ForceMode2D.Impulse);
			StartCoroutine(StunRoutine());
		}
		
		private IEnumerator StunRoutine()
		{
			IsStunned = true;
			// duration could be parameter?
			yield return new WaitForSeconds(0.5f);
			IsStunned = false;
		}

		private void Die()
		{
			if (m_anim != null) m_anim.SetBool(DieHash, true);

			m_rb.simulated = false;
			Collider2D col = GetComponent<Collider2D>();
			if (col != null) col.enabled = false;
			
			// Disable this script and potentially others?
			this.enabled = false; 
			
			// Optional: Notify other components?
			// For now, simpler to just destroy object
			Destroy(gameObject, 1.0f);
		}

		// --- Collision Logic (Stomp & Contact) ---

		private void OnCollisionEnter2D(Collision2D collision)
		{
			if (collision.gameObject.CompareTag("Player"))
			{
				PlayerController player = collision.gameObject.GetComponent<PlayerController>();
				if (player != null) HandleCollision(player);
			}
		}

		private void OnCollisionStay2D(Collision2D collision)
		{
			if (collision.gameObject.CompareTag("Player"))
			{
				PlayerController player = collision.gameObject.GetComponent<PlayerController>();
				if (player != null) HandleCollision(player);
			}
		}
		
		private void OnTriggerEnter2D(Collider2D collision)
		{
			if (collision.CompareTag("Player"))
			{
				PlayerController player = collision.GetComponent<PlayerController>();
				if (player != null) HandleCollision(player);
			}
		}

		private void HandleCollision(PlayerController player)
		{
			// Check for stomp
			Vector2 pVel = player.GetComponent<Rigidbody2D>().linearVelocity;
			bool isFalling = pVel.y < 0.1f; 
			bool isRising = pVel.y > 0.1f;
			bool isAbove = player.transform.position.y > transform.position.y + 0.3f; 

			if (isAbove && isRising) return;

			if (canBeStomped && isFalling && isAbove && !player.isHurt && !player.isGrounded && !player.isDashing)
			{
				TakeDamage(1, DamageSource.Stomp); 
				player.Bounce();
			}
			else
			{
				// Body Damage
				// If we are "Attacking", maybe we don't do body damage? (Optional logic from before)
				if (!IsAttacking)
				{
					player.TakeDamage(transform.position);
				}
			}
		}

		// --- UI Helpers ---

		private void CreateHealthBar()
		{
			GameObject root = new GameObject("HealthBar");
			root.transform.SetParent(transform);
			root.transform.localPosition = healthBarOffset;
			m_healthBarRoot = root.transform;

			Texture2D tex = new Texture2D(1, 1);
			tex.filterMode = FilterMode.Point;
			tex.SetPixel(0, 0, Color.white);
			tex.Apply();
			
			Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

			GameObject bg = new GameObject("Background");
			bg.transform.SetParent(root.transform);
			bg.transform.localPosition = Vector3.zero;
			bg.transform.localScale = new Vector3(healthBarSize.x, healthBarSize.y, 1f);
			SpriteRenderer bgSr = bg.AddComponent<SpriteRenderer>();
			bgSr.sprite = sprite;
			bgSr.color = Color.red;
			bgSr.sortingOrder = 100;

			GameObject fill = new GameObject("Fill");
			fill.transform.SetParent(root.transform);
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

		private IEnumerator FlashRoutine()
		{
			Color original = m_spriteRenderer.color;
			m_spriteRenderer.color = Color.red;
			yield return new WaitForSeconds(0.1f);
			m_spriteRenderer.color = original;
		}
	}
}
