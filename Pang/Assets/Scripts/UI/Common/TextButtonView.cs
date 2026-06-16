using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class TextButtonView : MonoBehaviour
{
	[SerializeField] private Button button = null;
	[SerializeField] private TextMeshProUGUI labelText = null;

	public Button Button => button;
	public TextMeshProUGUI LabelText => labelText;

	public void Configure(string label, UnityAction onClick)
	{
		if (labelText != null)
			labelText.text = label;

		if (button == null)
			return;

		button.onClick.RemoveAllListeners();
		if (onClick != null)
			button.onClick.AddListener(onClick);
	}
}
