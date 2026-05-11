using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI
{
	public class WindowResizeHandler : MonoBehaviour, IDragHandler
	{
		public enum ResizeDirection
		{
			TopLeft, TopRight, BottomLeft, BottomRight
		}

		[SerializeField] private RectTransform targetRect;
		[SerializeField] private ResizeDirection direction = ResizeDirection.BottomRight;
		[SerializeField] private Vector2 minSize = new Vector2(100, 100);

		private Canvas canvas;

		private void Awake()
		{
			if (targetRect == null)
			{
				var window = GetComponentInParent<UIWindow>();
				if (window != null)
					targetRect = window.RootRect;
				else
					targetRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
			}

			canvas = GetComponentInParent<Canvas>();
		}

		public void OnDrag(PointerEventData eventData)
		{
			if (canvas == null || targetRect == null) return;

			Vector2 delta = eventData.delta / canvas.scaleFactor;

			float oldWidth = targetRect.rect.width;
			float oldHeight = targetRect.rect.height;

			float widthSign = (direction == ResizeDirection.TopRight || direction == ResizeDirection.BottomRight) ? 1f : -1f;
			float heightSign = (direction == ResizeDirection.TopLeft || direction == ResizeDirection.TopRight) ? 1f : -1f;

			float newWidth = Mathf.Max(oldWidth + delta.x * widthSign, minSize.x);
			float newHeight = Mathf.Max(oldHeight + delta.y * heightSign, minSize.y);

			float actualDiffX = newWidth - oldWidth;
			float actualDiffY = newHeight - oldHeight;

			targetRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newWidth);
			targetRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newHeight);

			// Calculate which corner is fixed
			float fixedX = (widthSign > 0) ? 0f : 1f; // If resizing right, left (0) is fixed
			float fixedY = (heightSign > 0) ? 0f : 1f; // If resizing top, bottom (0) is fixed

			Vector2 pivot = targetRect.pivot;

			// If resizing right, fixedX=0. pivot.x - 0 is positive. actualDiffX is positive. Result positive (move right).
			// If resizing left, fixedX=1. pivot.x - 1 is negative. actualDiffX is positive. Result negative (move left).
			Vector2 posOffset = new Vector2(
				actualDiffX * (pivot.x - fixedX),
				actualDiffY * (pivot.y - fixedY)
			);

			targetRect.anchoredPosition += posOffset;
		}

		public void SetDirection(ResizeDirection dir)
		{
			direction = dir;
		}
	}
}

