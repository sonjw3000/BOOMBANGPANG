using TMPro;
using UnityEngine;

public sealed class AreaControlWindowContentView : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI statusText = null;
	[SerializeField] private TextButtonView createButton = null;
	[SerializeField] private RectTransform toggleRoot = null;

	public TextMeshProUGUI StatusText => statusText;
	public TextButtonView CreateButton => createButton;
	public RectTransform ToggleRoot => toggleRoot;
}
