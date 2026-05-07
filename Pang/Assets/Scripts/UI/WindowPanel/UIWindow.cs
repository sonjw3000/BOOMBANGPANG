using UnityEngine;

// 상단바 드래그
// 포커스시 최상단
// 포커스시 esc로 닫기
// 닫기버튼 존재
// 열림 닫힘 애니메이션

namespace Assets.Scripts.UI
{
	public class UIWindow : MonoBehaviour
	{
		[SerializeField] TMPro.TextMeshProUGUI titleText;
		[SerializeField] UnityEngine.UI.Image iconImage;
		[SerializeField] RectTransform contentRoot;
		[SerializeField] UnityEngine.UI.Button closeButton;
		[SerializeField] GameObject root;

		public RectTransform ContentRoot => contentRoot;

		private void Awake()
		{
			if (closeButton != null)
				closeButton?.onClick.AddListener(Close);

		}

		public void SetTitle(string title)
			=> titleText.text = title;

		public void SetIcon(Sprite icon)
		{
			iconImage.sprite = icon;
			iconImage.enabled = icon != null;
		}

		public void Open()
		{
			root.SetActive(true);
			
			// Reset scroll position to top if ScrollRect exists
			var scrollRect = GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
			if (scrollRect != null)
			{
				scrollRect.verticalNormalizedPosition = 1f;
			}
		}

		public void Close()
		{
			// todo
			// 사라지는 애니메이션 재생

			root.SetActive(false);
		}

	}
}
