using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI
{
	public class WindowDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
	{
		[SerializeField] private RectTransform targetRect;
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

		public void OnBeginDrag(PointerEventData eventData)
		{
			// Optional: Bring window to front
			if (targetRect != null)
				targetRect.SetAsLastSibling();
		}

		public void OnDrag(PointerEventData eventData)
		{
			if (canvas == null || targetRect == null) return;

			targetRect.anchoredPosition += eventData.delta / canvas.scaleFactor;
		}
	}
}
