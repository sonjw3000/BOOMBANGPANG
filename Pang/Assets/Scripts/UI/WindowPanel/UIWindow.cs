using UnityEngine;
using System.Collections.Generic;
using System;

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

		[Header("Tab System")]
		[SerializeField] private Transform tabRoot;
		[SerializeField] private GameObject tabButtonPrefab;
		private List<TabButton> tabButtons = new List<TabButton>();

		public RectTransform ContentRoot => contentRoot;

		private void Awake()
		{
			if (closeButton != null)
				closeButton?.onClick.AddListener(Close);
			
			UpdateTabAreaVisibility();
		}

		private void UpdateTabAreaVisibility()
		{
			if (tabRoot == null) return;
			
			bool hasTabs = tabButtons.Count > 0;
			
			// Ensure tabRoot is active if we have tabs
			tabRoot.gameObject.SetActive(true);
			
			// If tabRoot is inside a ScrollArea, hide the ScrollArea instead
			Transform area = tabRoot.parent != null && tabRoot.parent.name == "Viewport" ? tabRoot.parent.parent : tabRoot;
			
			if (area.gameObject.activeSelf != hasTabs)
			{
				area.gameObject.SetActive(hasTabs);
			}
			
			// Force layout rebuild
			if (hasTabs)
			{
				UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(area.GetComponent<RectTransform>());
			}
		}

		public void ClearTabs()
		{
			foreach (var btn in tabButtons)
			{
				if (btn != null) Destroy(btn.gameObject);
			}
			tabButtons.Clear();
			UpdateTabAreaVisibility();
		}

		public void AddTab(string label, Action<int> onSelect)
		{
			if (tabRoot == null)
			{
				Debug.LogWarning("TabRoot is null on " + gameObject.name);
				return;
			}
			if (tabButtonPrefab == null)
			{
				Debug.LogWarning("TabButtonPrefab is null on " + gameObject.name);
				return;
			}

			int index = tabButtons.Count;
			GameObject go = Instantiate(tabButtonPrefab, tabRoot);
			
			// Ensure transform is reset for layout group
			RectTransform rt = go.GetComponent<RectTransform>();
			if (rt != null)
			{
				rt.localPosition = Vector3.zero;
				rt.localScale = Vector3.one;
				rt.localRotation = Quaternion.identity;
			}

			TabButton tabBtn = go.GetComponent<TabButton>();
			if (tabBtn != null)
			{
				tabBtn.Init(index, label, (idx) => {
					onSelect?.Invoke(idx);
					UpdateTabVisuals(idx);
				});
				tabButtons.Add(tabBtn);
			}

			UpdateTabAreaVisibility();
		}

		public void UpdateTabVisuals(int selectedIndex)
		{
			for (int i = 0; i < tabButtons.Count; i++)
			{
				tabButtons[i].SetSelected(i == selectedIndex);
			}
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
