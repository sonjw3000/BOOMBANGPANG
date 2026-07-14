using System.Collections.Generic;
using Assets.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BuildingDetailLayoutView : MonoBehaviour
{
	[SerializeField] private GameObject overviewTab = null;
	[SerializeField] private GameObject facilitiesTab = null;
	[SerializeField] private GameObject policyTab = null;
	[SerializeField] private GameObject settingsTab = null;
	[SerializeField] private GameObject actionTab = null;
	[SerializeField] private WindowTabContentEntry[] tabContents = null;
	[SerializeField] private RectTransform summaryRoot = null;
	[SerializeField] private TextMeshProUGUI facilitiesSummaryText = null;
	[SerializeField] private TextMeshProUGUI policyHelpText = null;
	[SerializeField] private TextButtonView workScopeButton = null;
	[SerializeField] private TextMeshProUGUI demolitionNoteText = null;
	[SerializeField] private TextButtonView pendingDemolitionButton = null;
	[SerializeField] private TextButtonView restoreActiveButton = null;
	[SerializeField] private TextMeshProUGUI settingsStatusText = null;
	[SerializeField] private Toggle overrideThresholdToggle = null;
	[SerializeField] private Slider thresholdSlider = null;
	[SerializeField] private TextMeshProUGUI thresholdValueText = null;

	private readonly List<WindowTabContentEntry> fallbackTabContents = new();

	public GameObject OverviewTab => overviewTab;
	public GameObject FacilitiesTab => facilitiesTab;
	public GameObject PolicyTab => policyTab;
	public GameObject SettingsTab => settingsTab;
	public GameObject ActionTab => actionTab;
	public RectTransform SummaryRoot => summaryRoot;
	public TextMeshProUGUI FacilitiesSummaryText => facilitiesSummaryText;
	public TextMeshProUGUI PolicyHelpText => policyHelpText;
	public TextButtonView WorkScopeButton => workScopeButton;
	public TextMeshProUGUI DemolitionNoteText => demolitionNoteText;
	public TextButtonView PendingDemolitionButton => pendingDemolitionButton;
	public TextButtonView RestoreActiveButton => restoreActiveButton;
	public TextMeshProUGUI SettingsStatusText => settingsStatusText;
	public Toggle OverrideThresholdToggle => overrideThresholdToggle;
	public Slider ThresholdSlider => thresholdSlider;
	public TextMeshProUGUI ThresholdValueText => thresholdValueText;

	public IReadOnlyList<WindowTabContentEntry> GetTabContents()
	{
		if (tabContents != null && tabContents.Length > 0)
			return tabContents;

		fallbackTabContents.Clear();
		AddFallbackTab(WindowTabKind.Overview, "Overview", overviewTab);
		AddFallbackTab(WindowTabKind.Facilities, "Facilities", facilitiesTab);
		AddFallbackTab(WindowTabKind.Policy, "Policy", policyTab);
		AddFallbackTab(WindowTabKind.Settings, "Settings", settingsTab);
		AddFallbackTab(WindowTabKind.Action, "Action", actionTab);
		return fallbackTabContents;
	}

	private void AddFallbackTab(WindowTabKind kind, string label, GameObject contentRoot)
	{
		if (contentRoot == null)
			return;

		fallbackTabContents.Add(new WindowTabContentEntry(kind, label, contentRoot));
	}
}
