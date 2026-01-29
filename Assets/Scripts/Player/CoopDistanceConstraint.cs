using UnityEngine;

namespace SwampPreachers
{
	/// <summary>
	/// Constrains the distance between two players in co-op mode.
	/// Prevents players from moving too far apart.
	/// </summary>
	public class CoopDistanceConstraint : MonoBehaviour
	{
		[Header("Player References")]
		[SerializeField] private Transform player1;
		[SerializeField] private Transform player2;

		[Header("Distance Settings")]
		[Tooltip("Maximum distance players can be apart (0 = no constraint)")]
		[SerializeField] private float maxDistance = 20f;
		[Tooltip("Enable visual feedback when at max distance")]
		[SerializeField] private bool showDistanceWarning = true;

		private Rigidbody2D m_player1Rb;
		private Rigidbody2D m_player2Rb;
		private bool m_isConstrained = false;

		private void Start()
		{
			if (player1 != null)
				m_player1Rb = player1.GetComponent<Rigidbody2D>();
			if (player2 != null)
				m_player2Rb = player2.GetComponent<Rigidbody2D>();
		}

		private void LateUpdate()
		{
			if (player1 == null || player2 == null || maxDistance <= 0)
				return;

			// Calculate distance
			float distance = Vector3.Distance(player1.position, player2.position);

			// Check if exceeding max distance
			if (distance > maxDistance)
			{
				m_isConstrained = true;

				// Calculate the direction from player1 to player2
				Vector3 direction = (player2.position - player1.position).normalized;
				
				// Calculate the midpoint between players
				Vector3 midpoint = (player1.position + player2.position) / 2f;

				// Calculate constrained positions (half max distance from midpoint)
				float halfMaxDistance = maxDistance / 2f;
				Vector3 constrainedPos1 = midpoint - direction * halfMaxDistance;
				Vector3 constrainedPos2 = midpoint + direction * halfMaxDistance;

				// Apply constraints while preserving Y position if players are at different heights
				// Only constrain in the X direction (for side-scrolling)
				Vector3 newPos1 = player1.position;
				Vector3 newPos2 = player2.position;

				// Determine which player is trying to move away
				// Push them back to the constrained position on X axis
				if (Mathf.Abs(player1.position.x - constrainedPos1.x) > 0.1f)
				{
					newPos1.x = constrainedPos1.x;
					player1.position = newPos1;
					if (m_player1Rb != null)
						m_player1Rb.linearVelocity = new Vector2(0, m_player1Rb.linearVelocity.y);
				}

				if (Mathf.Abs(player2.position.x - constrainedPos2.x) > 0.1f)
				{
					newPos2.x = constrainedPos2.x;
					player2.position = newPos2;
					if (m_player2Rb != null)
						m_player2Rb.linearVelocity = new Vector2(0, m_player2Rb.linearVelocity.y);
				}
			}
			else
			{
				m_isConstrained = false;
			}
		}

		public void SetPlayers(Transform p1, Transform p2)
		{
			player1 = p1;
			player2 = p2;

			if (player1 != null)
				m_player1Rb = player1.GetComponent<Rigidbody2D>();
			if (player2 != null)
				m_player2Rb = player2.GetComponent<Rigidbody2D>();
		}

		public void SetMaxDistance(float distance)
		{
			maxDistance = distance;
		}

		// Debug visualization
		private void OnDrawGizmos()
		{
			if (player1 != null && player2 != null && maxDistance > 0)
			{
				// Draw line between players
				Gizmos.color = m_isConstrained ? Color.red : Color.green;
				Gizmos.DrawLine(player1.position, player2.position);

				// Draw max distance circles around each player
				Gizmos.color = Color.yellow;
				DrawCircle(player1.position, maxDistance / 2f, 20);
				DrawCircle(player2.position, maxDistance / 2f, 20);
			}
		}

		private void DrawCircle(Vector3 center, float radius, int segments)
		{
			float angleStep = 360f / segments;
			Vector3 prevPoint = center + new Vector3(radius, 0, 0);

			for (int i = 1; i <= segments; i++)
			{
				float angle = angleStep * i * Mathf.Deg2Rad;
				Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
				Gizmos.DrawLine(prevPoint, newPoint);
				prevPoint = newPoint;
			}
		}

		// Optional: On-screen warning
		private void OnGUI()
		{
			if (showDistanceWarning && m_isConstrained)
			{
				GUIStyle style = new GUIStyle(GUI.skin.label);
				style.fontSize = 20;
				style.normal.textColor = Color.red;
				style.alignment = TextAnchor.LowerCenter;
				style.fontStyle = FontStyle.Bold;

				GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height - 100, 300, 30), "Too far apart!", style);
			}
		}
	}
}
