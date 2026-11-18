using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SwampPreachers
{
	/// <summary>
	/// Manages playable party characters in Single-Player (tag-team switching between John and Toni)
	/// with Companion Follow AI toggle (F key) and temporary active indicator arrow.
	/// </summary>
	public class CharacterSwitcher : MonoBehaviour
	{
		[Header("Characters in Party")]
		[Tooltip("Assign John and Toni here, or leave empty to auto-detect PlayerControllers in scene.")]
		[SerializeField] private PlayerController[] characters;

		[Header("Settings")]
		[SerializeField] private int startingCharacterIndex = 0;
		[SerializeField] private bool allowSwitchingInAir = true;

		[Header("Companion Follow AI (Toggle F)")]
		[Tooltip("If true, the inactive companion automatically follows the active leader.")]
		[SerializeField] private bool followCompanion = true;
		[SerializeField] private float followDistance = 3.2f;
		[SerializeField] private float jumpObstacleCheckDistance = 0.7f;
		[SerializeField] private float catchUpDistance = 22f;
		[SerializeField] private float pitFallCatchUpDistance = 12f;
		[SerializeField] private float stuckTeleportTime = 6.0f;
		[SerializeField] private float respawnDropHeight = 1.0f;
		[SerializeField] private float respawnDropSpeed = -1.5f;
		[SerializeField] private LayerMask obstacleLayer;

		[Header("Visual Feedback (Temporary Arrow)")]
		[Tooltip("Duration in seconds the indicator arrow stays visible after switching.")]
		[SerializeField] private float indicatorDuration = 1.2f;
		[SerializeField] private Color indicatorColor = new Color(1f, 0.85f, 0.2f, 1f); // Vibrant Gold
		[SerializeField] private Vector3 indicatorOffset = new Vector3(0f, 1.35f, 0f);

		[Header("Camera Reference")]
		[SerializeField] private CameraFollow cameraFollow;

		public static CharacterSwitcher Instance { get; private set; }

		public int CurrentIndex => m_currentIndex;
		public PlayerController ActiveCharacter => (characters != null && characters.Length > m_currentIndex) ? characters[m_currentIndex] : null;
		public bool IsFollowing => followCompanion;

		public event System.Action<PlayerController> OnCharacterSwitched;
		public event System.Action<bool> OnFollowModeToggled;

		private int m_currentIndex = 0;
		private bool m_firstFrameApplied = false;

		// Companion AI tracking
		private float m_stuckTimer = 0f;
		private float m_companionJumpCooldown = 0f;
		private bool m_isCompanionMoving = false;
		private Vector3 m_lastCompanionPos;
		private Vector3 m_stuckCheckPos;
		private Coroutine m_indicatorCoroutine;
		private Coroutine m_delayedJumpCoroutine;
		private Coroutine m_delayedCrouchCoroutine;
		private bool m_targetCompanionCrouch;
		private GameObject m_activeIndicatorObj;

		private void Awake()
		{
			if (Instance == null) Instance = this;
			else if (Instance != this)
			{
				Destroy(gameObject);
				return;
			}
		}

		private void Start()
		{
			// Auto-find characters if none assigned
			if (characters == null || characters.Length == 0)
			{
				characters = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude);
				System.Array.Sort(characters, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
			}

			if (cameraFollow == null)
			{
				cameraFollow = FindAnyObjectByType<CameraFollow>();
			}

			if (obstacleLayer == 0)
			{
				obstacleLayer = LayerMask.GetMask("Ground", "Default");
			}

			// Ignore collision between players so they never push each other
			IgnorePlayerCollisions();

			// Validate starting index
			if (characters != null && characters.Length > 0)
			{
				m_currentIndex = Mathf.Clamp(startingCharacterIndex, 0, characters.Length - 1);
				ApplyCharacterState();
				ShowTemporaryIndicator(ActiveCharacter);
			}
		}

		private void Update()
		{
			// Enforce state on first frame to guarantee override over any PlayerController.Start()
			if (!m_firstFrameApplied)
			{
				m_firstFrameApplied = true;
				ApplyCharacterState();
				ShowTemporaryIndicator(ActiveCharacter);
			}

			if (characters == null || characters.Length <= 1) return;

			// 1. Check for character switch input (Tab or Q)
			if (GameInput.SwitchCharacter())
			{
				SwitchToNextCharacter();
			}

			// 2. Check for Follow / Stay toggle (F key)
			if (GameInput.ToggleFollow())
			{
				ToggleFollowMode();
			}

			// 3. Update Companion Follow AI
			UpdateCompanionAI();
		}

		public void ToggleFollowMode()
		{
			followCompanion = !followCompanion;
			Debug.Log($"[Companion AI] Mode toggled: {(followCompanion ? "FOLLOW (Active)" : "STAY (Still)")}");
			OnFollowModeToggled?.Invoke(followCompanion);

			if (!followCompanion)
			{
				PlayerController companion = GetCompanion();
				if (companion != null) companion.DoStop();
			}
		}

		private void UpdateCompanionAI()
		{
			if (ActiveCharacter == null) return;
			PlayerController companion = GetCompanion();
			if (companion == null) return;

			if (m_companionJumpCooldown > 0f)
			{
				m_companionJumpCooldown -= Time.deltaTime;
			}

			// 1. Synchronize Crouch with leader with organic reaction delay (80ms - 150ms)
			SynchronizeCompanionCrouch(companion);

			// 2. Synchronize Jump with leader with organic human-like delay (80ms - 150ms)
			if (followCompanion && GameInput.Jump() && companion.isGrounded && !companion.isCrouching && m_companionJumpCooldown <= 0f)
			{
				TriggerCompanionDelayedJump(companion);
			}

			if (!followCompanion)
			{
				companion.DoStop();
				m_stuckTimer = 0f;
				return;
			}

			Vector3 leaderPos = ActiveCharacter.transform.position;
			Vector3 compPos = companion.transform.position;

			float dx = leaderPos.x - compPos.x;
			float dy = leaderPos.y - compPos.y; // Positive = leader is higher; Negative = companion is higher
			float absDx = Mathf.Abs(dx);
			float leaderFacingDir = ActiveCharacter.transform.localScale.x > 0 ? 1f : -1f;

			// 1. Safety Catch-up:
			// A) Companion fell into a deep pit/hazard below leader (dy > pitFallCatchUpDistance)
			// B) Leader ran very far away horizontally (absDx > catchUpDistance)
			// (Only triggers when the leader is safely grounded!)
			if (ActiveCharacter.isGrounded && (absDx > catchUpDistance || dy > pitFallCatchUpDistance))
			{
				TeleportCompanionToLeader(companion, leaderPos);
				return;
			}

			// Check relative alignment
			// (dx * leaderFacingDir) < -0.1f means the companion is in FRONT of the leader in the direction the leader is facing
			bool isAheadOfLeader = (dx * leaderFacingDir) < -0.1f;
			bool leaderIsMoving = Mathf.Abs(ActiveCharacter.moveInput) > 0.1f || Mathf.Abs(ActiveCharacter.Velocity.x) > 0.5f;

			// 2. Horizontal Follow Movement with Hysteresis (prevents jitter / frame-by-frame starting and stopping):
			// - Start moving when distance > followDistance (e.g. 2.0m)
			// - Keep moving smoothly until close enough (followDistance * 0.7f, e.g. 1.4m)
			// - If companion happens to be ahead while leader is moving, stand still and let leader pass naturally
			float startDistance = followDistance;
			float stopDistance = followDistance * 0.7f;

			if (isAheadOfLeader && leaderIsMoving)
			{
				m_isCompanionMoving = false; // Chill in place, let leader pass naturally
			}
			else if (absDx > startDistance)
			{
				m_isCompanionMoving = true;
			}
			else if (absDx < stopDistance)
			{
				m_isCompanionMoving = false;
			}

			if (m_isCompanionMoving)
			{
				float moveDir = Mathf.Sign(dx);
				companion.DoMove(moveDir);

				// Stuck check: Only consider stuck if companion is trying to move, but hasn't advanced at least 0.4m over 6+ seconds
				// AND leader is far away (absDx > 7.0f) so we NEVER teleport when just slightly ahead of companion!
				if (companion.isWallClimbing || companion.isLedgeClimbing)
				{
					m_stuckTimer = 0f;
					m_stuckCheckPos = compPos;
				}
				else if (Vector2.Distance(compPos, m_stuckCheckPos) < 0.4f)
				{
					m_stuckTimer += Time.deltaTime;
					// Only teleport if companion has been completely stuck for a long time AND leader is well ahead
					if (m_stuckTimer >= stuckTeleportTime && absDx > 7.0f && ActiveCharacter.isGrounded)
					{
						TeleportCompanionToLeader(companion, leaderPos);
						m_stuckTimer = 0f;
						m_stuckCheckPos = compPos;
						return;
					}
				}
				else
				{
					m_stuckCheckPos = compPos;
					m_stuckTimer = 0f;
				}
				m_lastCompanionPos = compPos;

				// Obstacle & Jump checks:
				// If leader is below us (dy < -0.8f), companion SHOULD drop down naturally!
				bool leaderBelow = dy < -0.8f;
				bool leaderAbove = dy > 1.2f && ActiveCharacter.isGrounded;

				// A) Wall check ahead (low at 0.3m, high at 1.0m to anticipate obstacles)
				Vector2 lowRayOrigin = (Vector2)compPos + Vector2.up * 0.3f;
				Vector2 highRayOrigin = (Vector2)compPos + Vector2.up * 1.0f;
				RaycastHit2D lowWallHit = Physics2D.Raycast(lowRayOrigin, Vector2.right * moveDir, jumpObstacleCheckDistance, obstacleLayer);
				RaycastHit2D highWallHit = Physics2D.Raycast(highRayOrigin, Vector2.right * moveDir, jumpObstacleCheckDistance, obstacleLayer);

				// B) Pit / Gap check ahead (detect holes in ground before stepping into them)
				bool gapAhead = false;
				if (!leaderBelow)
				{
					Vector2 groundCheckAhead = (Vector2)compPos + (Vector2.right * moveDir * 0.7f) + (Vector2.down * 0.2f);
					RaycastHit2D groundHit = Physics2D.Raycast(groundCheckAhead, Vector2.down, 1.5f, obstacleLayer);
					gapAhead = groundHit.collider == null;
				}

				// Only hop over low obstacles (curbs/steps where lowRay hits but highRay is open)
				bool isLowStep = lowWallHit.collider != null && highWallHit.collider == null;

				// Ground jump check: strictly forbidden if companion is crouching!
				// Only jump if there is a low step to hop over, a gap to cross, or leader is on a ledge above.
				if (companion.isGrounded && !companion.isCrouching && m_companionJumpCooldown <= 0f)
				{
					if (isLowStep || gapAhead || leaderAbove)
					{
						companion.DoJump(allowAirJump: false);
						m_companionJumpCooldown = 0.35f;
					}
				}
				// Air double jump check: ONLY if already in the air, falling, and leader is STILL high above on an elevated ledge
				else if (!companion.isGrounded && !companion.isCrouching && m_companionJumpCooldown <= 0f && leaderAbove && dy > 2.2f && companion.Velocity.y < 0f)
				{
					companion.DoJump(allowAirJump: true);
					m_companionJumpCooldown = 0.5f;
				}
			}
			else
			{
				// Close enough: stop and idle
				companion.DoStop();
				m_stuckTimer = 0f;
				m_lastCompanionPos = compPos;
			}
		}

		private void TriggerCompanionDelayedJump(PlayerController companion)
		{
			if (m_delayedJumpCoroutine != null) return;
			m_delayedJumpCoroutine = StartCoroutine(CompanionDelayedJumpRoutine(companion));
		}

		private IEnumerator CompanionDelayedJumpRoutine(PlayerController companion)
		{
			// Subtle organic human-like reaction delay between 0.08s and 0.16s
			float delay = Random.Range(0.08f, 0.16f);
			yield return new WaitForSeconds(delay);

			if (companion != null && companion.isGrounded && !companion.isCrouching && followCompanion && m_companionJumpCooldown <= 0f)
			{
				companion.DoJump(allowAirJump: false);
				m_companionJumpCooldown = 0.35f;
			}
			m_delayedJumpCoroutine = null;
		}

		private void SynchronizeCompanionCrouch(PlayerController companion)
		{
			if (ActiveCharacter == null || companion == null) return;
			bool leaderCrouching = ActiveCharacter.isCrouching;

			if (leaderCrouching != companion.isCrouching)
			{
				if (m_delayedCrouchCoroutine == null || m_targetCompanionCrouch != leaderCrouching)
				{
					if (m_delayedCrouchCoroutine != null)
					{
						StopCoroutine(m_delayedCrouchCoroutine);
					}
					m_targetCompanionCrouch = leaderCrouching;
					m_delayedCrouchCoroutine = StartCoroutine(CompanionDelayedCrouchRoutine(companion, leaderCrouching));
				}
			}
			else
			{
				if (m_delayedCrouchCoroutine != null && m_targetCompanionCrouch == leaderCrouching)
				{
					StopCoroutine(m_delayedCrouchCoroutine);
					m_delayedCrouchCoroutine = null;
				}
			}
		}

		private IEnumerator CompanionDelayedCrouchRoutine(PlayerController companion, bool targetCrouch)
		{
			// Subtle organic human reaction delay between 0.08s and 0.15s (matching jump delay)
			float delay = Random.Range(0.08f, 0.15f);
			yield return new WaitForSeconds(delay);

			if (companion != null)
			{
				companion.DoCrouch(targetCrouch);
			}
			m_delayedCrouchCoroutine = null;
		}

		private void TeleportCompanionToLeader(PlayerController companion, Vector3 leaderPos)
		{
			// CRITICAL: Do NOT teleport if leader is in the air (wall-jumping, falling, etc.)
			// Wait until leader has safely landed on solid ground!
			if (ActiveCharacter == null || !ActiveCharacter.isGrounded)
			{
				return;
			}

			float dir = ActiveCharacter.transform.localScale.x > 0 ? 1f : -1f;
			Vector3 safeSpawnPos = FindSafeCompanionSpawnPos(leaderPos, dir);

			companion.transform.position = safeSpawnPos;
			
			// Give initial mid-fall downward velocity so it feels like dropping from the sky
			Rigidbody2D compRb = companion.GetComponent<Rigidbody2D>();
			if (compRb != null)
			{
				compRb.linearVelocity = new Vector2(0f, respawnDropSpeed);
			}

			companion.DoStop();
			m_stuckTimer = 0f;
			m_lastCompanionPos = companion.transform.position;
		}

		private Vector3 FindSafeCompanionSpawnPos(Vector3 leaderPos, float leaderFacingDir)
		{
			LayerMask groundMask = obstacleLayer != 0 ? obstacleLayer : LayerMask.GetMask("Ground", "Default");

			// 1. Try several safe positions BEHIND the leader
			float[] behindDistances = new float[] { 1.6f, 1.1f, 0.6f };
			foreach (float dist in behindDistances)
			{
				Vector2 testPos = (Vector2)leaderPos + new Vector2(-leaderFacingDir * dist, 0.4f);
				RaycastHit2D hit = Physics2D.Raycast(testPos, Vector2.down, 3.5f, groundMask);
				if (hit.collider != null)
				{
					return new Vector3(testPos.x, leaderPos.y + respawnDropHeight, 0f);
				}
			}

			// 2. Fallback: Directly at leader's safe X coordinate (NEVER in front of leader!)
			return new Vector3(leaderPos.x, leaderPos.y + respawnDropHeight, 0f);
		}

		public PlayerController GetCompanion()
		{
			if (characters == null || characters.Length <= 1) return null;
			int companionIndex = (m_currentIndex == 0) ? 1 : 0;
			return (companionIndex < characters.Length) ? characters[companionIndex] : null;
		}

		public void SwitchToNextCharacter()
		{
			if (characters == null || characters.Length <= 1) return;

			if (ActiveCharacter != null && (!allowSwitchingInAir && !ActiveCharacter.isGrounded))
			{
				return;
			}

			int nextIndex = (m_currentIndex + 1) % characters.Length;
			SwitchToCharacter(nextIndex);
		}

		public void SwitchToCharacter(int targetIndex)
		{
			if (characters == null || targetIndex < 0 || targetIndex >= characters.Length) return;
			if (targetIndex == m_currentIndex) return;

			// Stop previous leader
			if (ActiveCharacter != null) ActiveCharacter.DoStop();

			m_currentIndex = targetIndex;
			ApplyCharacterState();

			// Show temporary arrow above newly active character
			ShowTemporaryIndicator(ActiveCharacter);

			OnCharacterSwitched?.Invoke(ActiveCharacter);
		}

		private void ApplyCharacterState()
		{
			for (int i = 0; i < characters.Length; i++)
			{
				if (characters[i] == null) continue;

				bool isActive = (i == m_currentIndex);
				characters[i].SetPlayable(isActive);
			}

			// Update Camera
			if (cameraFollow != null && ActiveCharacter != null)
			{
				cameraFollow.SetTarget(ActiveCharacter.transform);
			}
		}

		private void ShowTemporaryIndicator(PlayerController target)
		{
			if (target == null) return;

			if (m_indicatorCoroutine != null)
			{
				StopCoroutine(m_indicatorCoroutine);
			}

			m_indicatorCoroutine = StartCoroutine(IndicatorRoutine(target.transform));
		}

		private static Sprite s_indicatorSprite;

		private static Sprite GetIndicatorSprite()
		{
			if (s_indicatorSprite != null) return s_indicatorSprite;

			int size = 32;
			Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
			tex.filterMode = FilterMode.Bilinear;
			Color transparent = new Color(0, 0, 0, 0);
			Color white = Color.white;

			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float halfWidthAtY = ((float)y / (float)size) * (size * 0.45f);
					float midX = (size - 1) * 0.5f;
					if (Mathf.Abs(x - midX) <= halfWidthAtY && y >= 2)
					{
						tex.SetPixel(x, y, white);
					}
					else
					{
						tex.SetPixel(x, y, transparent);
					}
				}
			}
			tex.Apply();
			s_indicatorSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0f), 32);
			return s_indicatorSprite;
		}

		private IEnumerator IndicatorRoutine(Transform targetTransform)
		{
			if (m_activeIndicatorObj == null)
			{
				m_activeIndicatorObj = new GameObject("ActivePlayerIndicator");
				m_activeIndicatorObj.transform.SetParent(transform);

				SpriteRenderer sr = m_activeIndicatorObj.AddComponent<SpriteRenderer>();
				sr.sprite = GetIndicatorSprite();
				sr.sortingLayerName = "UI";
				sr.sortingOrder = 1000;
				m_activeIndicatorObj.transform.localScale = Vector3.one * 0.75f;
			}

			m_activeIndicatorObj.SetActive(true);
			SpriteRenderer spriteRenderer = m_activeIndicatorObj.GetComponent<SpriteRenderer>();

			float elapsed = 0f;

			while (elapsed < indicatorDuration && targetTransform != null)
			{
				elapsed += Time.deltaTime;
				float progress = elapsed / indicatorDuration;

				// Gentle bobbing effect (starts slightly higher and drops into place)
				float bounce = Mathf.Sin(progress * Mathf.PI * 2f) * 0.15f * (1f - progress);
				m_activeIndicatorObj.transform.position = targetTransform.position + indicatorOffset + new Vector3(0f, bounce, 0f);

				// Smooth fade out in the last 35% of duration
				if (spriteRenderer != null)
				{
					Color c = indicatorColor;
					if (progress > 0.65f)
					{
						c.a = Mathf.Lerp(1f, 0f, (progress - 0.65f) / 0.35f);
					}
					spriteRenderer.color = c;
				}

				yield return null;
			}

			if (m_activeIndicatorObj != null)
			{
				m_activeIndicatorObj.SetActive(false);
			}
			m_indicatorCoroutine = null;
		}

		private void IgnorePlayerCollisions()
		{
			if (characters == null || characters.Length < 2) return;

			for (int i = 0; i < characters.Length; i++)
			{
				if (characters[i] == null) continue;
				Collider2D colA = characters[i].GetComponent<Collider2D>();
				if (colA == null) continue;

				for (int j = i + 1; j < characters.Length; j++)
				{
					if (characters[j] == null) continue;
					Collider2D colB = characters[j].GetComponent<Collider2D>();
					if (colB == null) continue;

					Physics2D.IgnoreCollision(colA, colB, true);
				}
			}
		}

		private void OnDestroy()
		{
			if (m_activeIndicatorObj != null)
			{
				Destroy(m_activeIndicatorObj);
			}
		}
	}
}
