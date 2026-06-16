using TMPro;
using UnityEngine;

public sealed class BuildingDetailLayoutView : MonoBehaviour
{
	[SerializeField] private GameObject overviewTab = null;
	[SerializeField] private GameObject facilitiesTab = null;
	[SerializeField] private GameObject policyTab = null;
	[SerializeField] private GameObject zonesTab = null;
	[SerializeField] private GameObject actionTab = null;
	[SerializeField] private RectTransform summaryRoot = null;
	[SerializeField] private TextMeshProUGUI facilitiesSummaryText = null;
	[SerializeField] private TextMeshProUGUI policyHelpText = null;
	[SerializeField] private TextButtonView workScopeButton = null;
	[SerializeField] private TextMeshProUGUI zoneStatusText = null;
	[SerializeField] private TextButtonView zoneOpenControlsButton = null;
	[SerializeField] private RectTransform zoneListRoot = null;
	[SerializeField] private TextMeshProUGUI zoneEmptyText = null;
	[SerializeField] private TextMeshProUGUI demolitionNoteText = null;
	[SerializeField] private TextButtonView pendingDemolitionButton = null;
	[SerializeField] private TextButtonView restoreActiveButton = null;

	public GameObject OverviewTab => overviewTab;
	public GameObject FacilitiesTab => facilitiesTab;
	public GameObject PolicyTab => policyTab;
	public GameObject ZonesTab => zonesTab;
	public GameObject ActionTab => actionTab;
	public RectTransform SummaryRoot => summaryRoot;
	public TextMeshProUGUI FacilitiesSummaryText => facilitiesSummaryText;
	public TextMeshProUGUI PolicyHelpText => policyHelpText;
	public TextButtonView WorkScopeButton => workScopeButton;
	public TextMeshProUGUI ZoneStatusText => zoneStatusText;
	public TextButtonView ZoneOpenControlsButton => zoneOpenControlsButton;
	public RectTransform ZoneListRoot => zoneListRoot;
	public TextMeshProUGUI ZoneEmptyText => zoneEmptyText;
	public TextMeshProUGUI DemolitionNoteText => demolitionNoteText;
	public TextButtonView PendingDemolitionButton => pendingDemolitionButton;
	public TextButtonView RestoreActiveButton => restoreActiveButton;
}
