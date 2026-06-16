using TMPro;
using UnityEngine;

public sealed class DetailInfoRowView : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI labelText = null;
	[SerializeField] private TextMeshProUGUI valueText = null;

	public TextMeshProUGUI LabelText => labelText;
	public TextMeshProUGUI ValueText => valueText;

	public void SetLabel(string label)
	{
		if (labelText != null)
			labelText.text = label;
	}
}
