using TMPro;
using UnityEngine;

public sealed class FacilityRuleWindowView : MonoBehaviour
{
	[SerializeField] private TextButtonView createPresetButton = null;
	[SerializeField] private TextButtonView cancelApplyModeButton = null;
	[SerializeField] private TextRowView statusRow = null;
	[SerializeField] private FacilityRulePresetRowView[] presetRows = null;

	public TextButtonView CreatePresetButton => createPresetButton;
	public TextButtonView CancelApplyModeButton => cancelApplyModeButton;
	public TextRowView StatusRow => statusRow;
	public FacilityRulePresetRowView[] PresetRows => presetRows;
}
