using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SwampPreachers
{
	public class CameraFollow : MonoBehaviour
	{
	    [SerializeField]
		private Transform target;
		[SerializeField]
		private float smoothSpeed = 0.125f;
		public Vector3 offset;
		
		[Header("Camera bounds")]
		public Vector3 minCamerabounds;
		public Vector3 maxCamerabounds;

		[Header("Dynamic Zoom (Co-op)")]
		[Tooltip("Enable dynamic zoom based on player distance")]
		public bool enableDynamicZoom = false;
		[Tooltip("Reference to both players for distance calculation")]
		public Transform player1;
		public Transform player2;
		[Tooltip("Minimum camera orthographic size")]
		public float minZoom = 5f;
		[Tooltip("Maximum camera orthographic size")]
		public float maxZoom = 15f;
		[Tooltip("How much to zoom out per unit of distance between players")]
		public float zoomOutFactor = 0.5f;
		[Tooltip("Additional padding for zoom calculation")]
		public float zoomPadding = 2f;
		[Tooltip("Speed of zoom transitions")]
		public float zoomSmoothSpeed = 2f;

		private Camera m_camera;
		private float m_targetZoom;
		private Vector3 m_lookOffset;

		private void Start()
		{
			m_camera = GetComponent<Camera>();
			if (m_camera != null)
			{
				m_targetZoom = m_camera.orthographicSize;
			}
		}

		private void FixedUpdate()
		{
			// Position following
			Vector3 desiredPosition = target.localPosition + offset + m_lookOffset;
			var localPosition = transform.localPosition;
			Vector3 smoothedPosition = Vector3.Lerp(localPosition, desiredPosition, smoothSpeed);
			localPosition = smoothedPosition;

			// clamp camera's position between min and max
			localPosition = new Vector3(
				Mathf.Clamp(localPosition.x, minCamerabounds.x, maxCamerabounds.x),
				Mathf.Clamp(localPosition.y, minCamerabounds.y, maxCamerabounds.y),
				Mathf.Clamp(localPosition.z, minCamerabounds.z, maxCamerabounds.z)
				);
			transform.localPosition = localPosition;

			// Dynamic Zoom
			if (enableDynamicZoom && m_camera != null && player1 != null && player2 != null)
			{
				UpdateDynamicZoom();
			}
		}

		private void UpdateDynamicZoom()
		{
			// Calculate distance between players
			float distance = Vector3.Distance(player1.position, player2.position);

			// Calculate required zoom to fit both players
			// We use distance + padding to ensure players aren't at screen edges
			m_targetZoom = (distance * zoomOutFactor) + zoomPadding;

			// Clamp zoom between min and max
			m_targetZoom = Mathf.Clamp(m_targetZoom, minZoom, maxZoom);

			// Smoothly interpolate to target zoom
			if (m_camera.orthographicSize != m_targetZoom)
			{
				m_camera.orthographicSize = Mathf.Lerp(
					m_camera.orthographicSize,
					m_targetZoom,
					zoomSmoothSpeed * Time.fixedDeltaTime
				);
			}
		}

		public void SetTarget(Transform targetToSet)
		{
			target = targetToSet;
		}

		public void SetLookOffset(Vector3 offset)
		{
			m_lookOffset = offset;
		}

		// Public methods for CoopCameraManager
		public void EnableDynamicZoom(bool enable)
		{
			enableDynamicZoom = enable;
		}

		public void SetPlayers(Transform p1, Transform p2)
		{
			player1 = p1;
			player2 = p2;
		}

		public void ResetZoom()
		{
			if (m_camera != null)
			{
				m_camera.orthographicSize = minZoom;
			}
		}
	}
}
