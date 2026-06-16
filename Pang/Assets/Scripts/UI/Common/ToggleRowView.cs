using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ToggleRowView : MonoBehaviour
{
	[SerializeField] private Toggle toggle = null;
	[SerializeField] private TextMeshProUGUI labelText = null;
	[SerializeField] private Image background = null;

	public Toggle Toggle => toggle;
	public TextMeshProUGUI LabelText => labelText;
	public Image Background => background;
}
