using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;
using UnityEngine.EventSystems;

// 상단바 드래그
// 포커스시 최상단
// 포커스시 esc로 닫기
// 닫기버튼 존재
// 열림 닫힘 애니메이션

namespace UI.Windows
{
	public class UIWindow : MonoBehaviour, IPointerDownHandler
	{
		[SerializeField] private RectTransform windowRoot;
		[SerializeField] private RectTransform headDragUpperBar;
		//[SerializeField] private UIButtonBridge closeButton;

		[SerializeField] private bool closable = true;

		public RectTransform Rect => windowRoot ? windowRoot : (RectTransform)transform;
		public bool Closable => closable;

		protected virtual void Awake()
		{
			if (windowRoot == null) windowRoot = GetComponent<RectTransform>();

			//if (closeButton != null)

			if (headDragUpperBar != null)
			{
				var drag = headDragUpperBar.GetComponent<RectTransform>();
			}
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			// todo
			// 해당 창을 포커싱 해주기 위해 매니저에 뭔가를 해줘야함
		}

	}
}
