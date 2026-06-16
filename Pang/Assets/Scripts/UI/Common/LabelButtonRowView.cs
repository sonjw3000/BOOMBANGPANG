using TMPro;
using UnityEngine;

public sealed class LabelButtonRowView : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI labelText = null;
	[SerializeField] private TextButtonView actionButton = null;

	public TextMeshProUGUI LabelText => labelText;
	public TextButtonView ActionButton => actionButton;
}
