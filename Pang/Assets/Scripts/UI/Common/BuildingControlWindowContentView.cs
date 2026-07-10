using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BuildingControlWindowContentView : MonoBehaviour
{
	[SerializeField] private GameObject overviewTab = null;
	[SerializeField] private GameObject operationsTab = null;
	[SerializeField] private GameObject actionTab = null;
	[SerializeField] private TextMeshProUGUI overviewStatusText = null;
	[SerializeField] private TextMeshProUGUI overviewSummaryText = null;
	[SerializeField] private TextMeshProUGUI operationsStatusText = null;
	[SerializeField] private RectTransform buildingListRoot = null;
	[SerializeField] private TextMeshProUGUI buildingListEmptyText = null;
	[SerializeField] private TextMeshProUGUI actionStatusText = null;
	[SerializeField] private Dropdown buildingTypeDropdown = null;
	[SerializeField] private Dropdown footprintPresetDropdown = null;
	[SerializeField] private TextButtonView createButton = null;
	[SerializeField] private TextButtonView linkCargoPortsButton = null;

	public GameObject OverviewTab => overviewTab;
	public GameObject OperationsTab => operationsTab;
	public GameObject ActionTab => actionTab;
	public TextMeshProUGUI OverviewStatusText => overviewStatusText;
	public TextMeshProUGUI OverviewSummaryText => overviewSummaryText;
	public TextMeshProUGUI OperationsStatusText => operationsStatusText;
	public RectTransform BuildingListRoot => buildingListRoot;
	public TextMeshProUGUI BuildingListEmptyText => buildingListEmptyText;
	public TextMeshProUGUI ActionStatusText => actionStatusText;
	public Dropdown BuildingTypeDropdown => buildingTypeDropdown;
	public Dropdown FootprintPresetDropdown => footprintPresetDropdown;
	public TextButtonView CreateButton => createButton;
	public TextButtonView LinkCargoPortsButton => linkCargoPortsButton;
}
