using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DropdownRowView : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI labelText = null;
	[SerializeField] private Dropdown dropdown = null;

	public TextMeshProUGUI LabelText => labelText;
	public Dropdown Dropdown => dropdown;
}
