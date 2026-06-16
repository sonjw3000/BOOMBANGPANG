using TMPro;
using UnityEngine;

public sealed class ZoneDetailLayoutView : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI nameText = null;
	[SerializeField] private TextMeshProUGUI typeText = null;
	[SerializeField] private TextMeshProUGUI boundsText = null;
	[SerializeField] private TextMeshProUGUI facilitiesHeaderText = null;
	[SerializeField] private RectTransform facilitiesListRoot = null;
	[SerializeField] private TextMeshProUGUI facilitiesPlaceholderText = null;

	public TextMeshProUGUI NameText => nameText;
	public TextMeshProUGUI TypeText => typeText;
	public TextMeshProUGUI BoundsText => boundsText;
	public TextMeshProUGUI FacilitiesHeaderText => facilitiesHeaderText;
	public RectTransform FacilitiesListRoot => facilitiesListRoot;
	public TextMeshProUGUI FacilitiesPlaceholderText => facilitiesPlaceholderText;
}
