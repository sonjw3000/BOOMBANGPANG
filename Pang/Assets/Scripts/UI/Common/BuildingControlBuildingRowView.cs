using TMPro;
using UnityEngine;

public sealed class BuildingControlBuildingRowView : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI labelText = null;
	[SerializeField] private TextButtonView scopeButton = null;
	[SerializeField] private TextButtonView detailsButton = null;

	public TextMeshProUGUI LabelText => labelText;
	public TextButtonView ScopeButton => scopeButton;
	public TextButtonView DetailsButton => detailsButton;
}
