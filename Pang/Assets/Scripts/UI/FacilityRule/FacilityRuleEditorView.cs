using UnityEngine;

public sealed class FacilityRuleEditorView : MonoBehaviour
{
	[SerializeField] private DropdownRowView priorityDropdownRow = null;
	[SerializeField] private DropdownRowView workerKindDropdownRow = null;
	[SerializeField] private DropdownRowView requiredItemStatusDropdownRow = null;
	[SerializeField] private MultiSelectDropdownRowView requiredItemTagsDropdown = null;
	[SerializeField] private MultiSelectDropdownRowView forbiddenItemTagsDropdown = null;
	[SerializeField] private MultiSelectDropdownRowView requiredWorkerAbilitiesDropdown = null;
	[SerializeField] private MultiSelectDropdownRowView requiredHumanTypesDropdown = null;
	[SerializeField] private MultiSelectDropdownRowView forbiddenHumanTypesDropdown = null;
	[SerializeField] private MultiSelectDropdownRowView requiredRobotTypesDropdown = null;
	[SerializeField] private MultiSelectDropdownRowView forbiddenRobotTypesDropdown = null;
	[SerializeField] private MultiSelectDropdownRowView requiredDestinationsDropdown = null;

	[SerializeField] private TextRowView whiteListSummaryRow = null;
	[SerializeField] private TextRowView blackListSummaryRow = null;
	[SerializeField] private TextButtonView clearWhiteListButton = null;
	[SerializeField] private TextButtonView clearBlackListButton = null;
	[SerializeField] private TextButtonView resetRuleButton = null;

	public DropdownRowView PriorityDropdownRow => priorityDropdownRow;
	public DropdownRowView WorkerKindDropdownRow => workerKindDropdownRow;
	public DropdownRowView RequiredItemStatusDropdownRow => requiredItemStatusDropdownRow;
	public MultiSelectDropdownRowView RequiredItemTagsDropdown => requiredItemTagsDropdown;
	public MultiSelectDropdownRowView ForbiddenItemTagsDropdown => forbiddenItemTagsDropdown;
	public MultiSelectDropdownRowView RequiredWorkerAbilitiesDropdown => requiredWorkerAbilitiesDropdown;
	public MultiSelectDropdownRowView RequiredHumanTypesDropdown => requiredHumanTypesDropdown;
	public MultiSelectDropdownRowView ForbiddenHumanTypesDropdown => forbiddenHumanTypesDropdown;
	public MultiSelectDropdownRowView RequiredRobotTypesDropdown => requiredRobotTypesDropdown;
	public MultiSelectDropdownRowView ForbiddenRobotTypesDropdown => forbiddenRobotTypesDropdown;
	public MultiSelectDropdownRowView RequiredDestinationsDropdown => requiredDestinationsDropdown;
	public TextRowView WhiteListSummaryRow => whiteListSummaryRow;
	public TextRowView BlackListSummaryRow => blackListSummaryRow;
	public TextButtonView ClearWhiteListButton => clearWhiteListButton;
	public TextButtonView ClearBlackListButton => clearBlackListButton;
	public TextButtonView ResetRuleButton => resetRuleButton;
}
