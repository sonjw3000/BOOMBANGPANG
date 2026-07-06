using UnityEngine;

public sealed class FacilityRuleFacilityListWindowView : MonoBehaviour
{
	[SerializeField] private TextRowView statusRow = null;
	[SerializeField] private TextRowView[] facilityRows = null;

	public TextRowView StatusRow => statusRow;
	public TextRowView[] FacilityRows => facilityRows;
}
