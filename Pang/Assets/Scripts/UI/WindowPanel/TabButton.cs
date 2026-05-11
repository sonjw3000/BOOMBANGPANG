using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

namespace Assets.Scripts.UI
{
	public class TabButton : MonoBehaviour
	{
		[SerializeField] private TextMeshProUGUI labelText;
		[SerializeField] private Image background;
		[SerializeField] private Button button;

		[Header("Settings")]
		public Color selectedColor = Color.white;
		public Color unselectedColor = Color.grey;

		private Action<int> onClick;
		private int tabIndex;

		public void Init(int index, string label, Action<int> callback)
		{
			tabIndex = index;
			labelText.text = label;
			onClick = callback;
			button.onClick.AddListener(() => onClick?.Invoke(tabIndex));
		}

		public void SetSelected(bool selected)
		{
			background.color = selected ? selectedColor : unselectedColor;
			// Optionally change text color or style
			labelText.fontWeight = selected ? FontWeight.Bold : FontWeight.Regular;
		}
	}
}
