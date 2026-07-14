using TMPro;
using UnityEngine;

public sealed class AreaDetailLayoutView : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI nameText = null;
	[SerializeField] private TextMeshProUGUI typeText = null;
	[SerializeField] private TextMeshProUGUI boundsText = null;
	[SerializeField] private TextMeshProUGUI facilitiesHeaderText = null;
	[SerializeField] private RectTransform facilitiesListRoot = null;
	[SerializeField] private LabelButtonRowView[] facilityRows = null;
	[SerializeField] private TextMeshProUGUI facilitiesPlaceholderText = null;

	public TextMeshProUGUI NameText => nameText;
	public TextMeshProUGUI TypeText => typeText;
	public TextMeshProUGUI BoundsText => boundsText;
	public TextMeshProUGUI FacilitiesHeaderText => facilitiesHeaderText;
	public RectTransform FacilitiesListRoot => facilitiesListRoot;
	public LabelButtonRowView[] FacilityRows => facilityRows;
	public TextMeshProUGUI FacilitiesPlaceholderText => facilitiesPlaceholderText;

	public void HideLegacyFacilitySection()
	{
		facilitiesHeaderText?.gameObject.SetActive(false);
		facilitiesListRoot?.gameObject.SetActive(false);
		facilitiesPlaceholderText?.gameObject.SetActive(false);
	}
}
