using UnityEngine;

namespace SwampPreachers
{
	/// <summary>
	/// Automatically positions this GameObject at the midpoint between two player transforms.
	/// The camera should follow this object to keep both players visible.
	/// </summary>
	public class CameraMidpointTarget : MonoBehaviour
	{
		[Header("Player References")]
		[SerializeField] private Transform player1;
		[SerializeField] private Transform player2;

		[Header("Settings")]
		[SerializeField] private float smoothSpeed = 5f;
		[Tooltip("Offset from the midpoint (useful for vertical adjustment)")]
		[SerializeField] private Vector3 midpointOffset = Vector3.zero;

		[Header("Fallback Behavior")]
		[Tooltip("If true, follows only the alive player when one dies")]
		[SerializeField] private bool followSinglePlayerOnDeath = true;

		private void LateUpdate()
		{
			// Check if both players exist
			bool player1Exists = player1 != null;
			bool player2Exists = player2 != null;

			Vector3 targetPosition;

			if (player1Exists && player2Exists)
			{
				// Both players alive: calculate midpoint
				targetPosition = (player1.position + player2.position) / 2f + midpointOffset;
			}
			else if (followSinglePlayerOnDeath)
			{
				// One player dead: follow the alive player
				if (player1Exists)
					targetPosition = player1.position + midpointOffset;
				else if (player2Exists)
					targetPosition = player2.position + midpointOffset;
				else
					return; // Both dead, don't move
			}
			else
			{
				// Both players required, but at least one is missing
				return;
			}

			// Smooth movement to target position
			if (smoothSpeed > 0)
			{
				transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
			}
			else
			{
				transform.position = targetPosition;
			}
		}

		// Public methods for dynamic player assignment
		public void SetPlayers(Transform p1, Transform p2)
		{
			player1 = p1;
			player2 = p2;
		}

		public void SetPlayer1(Transform p1)
		{
			player1 = p1;
		}

		public void SetPlayer2(Transform p2)
		{
			player2 = p2;
		}

		// For debugging in the Scene view
		private void OnDrawGizmos()
		{
			if (player1 != null && player2 != null)
			{
				Gizmos.color = Color.yellow;
				Gizmos.DrawLine(player1.position, player2.position);
				Gizmos.DrawWireSphere(transform.position, 0.5f);
			}
		}
	}
}
