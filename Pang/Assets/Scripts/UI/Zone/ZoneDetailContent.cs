using System.Text;
using Assets.Scripts.UI;
using UnityEngine;

public class ZoneDetailContent : DetailContent<ZoneSelectionProxy>
{
	protected override bool UseDefaultTabs => false;

	private enum ZoneDetailTab
	{
		Info,
		Action,
	}

	[SerializeField] private RectTransform infoTabRoot = null;
	[SerializeField] private RectTransform actionTabRoot = null;
	[SerializeField] private ZoneDetailLayoutView layoutView = null;
	[SerializeField] private TextButtonView deleteZoneButton = null;

	private SelectionUIMaster selectionUIMaster;
	private UIWindow window;
	private string lastFacilityListSignature;

	protected override void LinkData()
	{
		EnsureWindow();
		BindActionTab();
		SetupTabs();
		SetTab((int)ZoneDetailTab.Info);
		UpdateData();
	}

	protected override void UpdateData()
	{
		ZoneArea zone = GetZone();
		if (zone == null)
			return;

		UpdateInfoTab(zone);
	}

	private void EnsureWindow()
	{
		if (window == null)
			window = GetComponentInParent<UIWindow>(true);
	}

	private void SetupTabs()
	{
		if (window == null)
			return;

		window.ClearTabs();
		window.AddTab("Info", SetTab);
		window.AddTab("Action", SetTab);
		window.UpdateTabVisuals(0);
	}

	private void SetTab(int tabIndex)
	{
		if (infoTabRoot != null)
			infoTabRoot.gameObject.SetActive(tabIndex == (int)ZoneDetailTab.Info);
		if (actionTabRoot != null)
			actionTabRoot.gameObject.SetActive(tabIndex == (int)ZoneDetailTab.Action);

		window?.UpdateTabVisuals(tabIndex);
	}

	private void BindActionTab()
	{
		deleteZoneButton?.Configure("Delete Zone", () => provider?.DeleteObject());
	}

	private void UpdateInfoTab(ZoneArea zone)
	{
		if (layoutView == null)
			return;

		if (layoutView.NameText != null)
			layoutView.NameText.text = zone.DisplayName;
		if (layoutView.TypeText != null)
			layoutView.TypeText.text = zone.Type.ToString();
		if (layoutView.BoundsText != null)
		{
			RectInt bounds = zone.Bounds;
			layoutView.BoundsText.text = $"Bounds: {bounds.width}x{bounds.height} @ {bounds.xMin}, {bounds.yMin}  Floor: {zone.Floor}";
		}
		if (layoutView.FacilitiesHeaderText != null)
			layoutView.FacilitiesHeaderText.text = "Facilities";
		if (layoutView.FacilitiesPlaceholderText != null)
			layoutView.FacilitiesPlaceholderText.text = BuildFacilitiesPlaceholder(zone);

		RefreshFacilityRowsIfNeeded(zone);
	}

	private void RefreshFacilityRowsIfNeeded(ZoneArea zone)
	{
		string signature = BuildFacilityListSignature(zone);
		if (lastFacilityListSignature == signature)
			return;

		ApplyFacilityRows(zone);
		lastFacilityListSignature = signature;
	}

	private void ApplyFacilityRows(ZoneArea zone)
	{
		if (layoutView == null)
			return;

		LabelButtonRowView[] rows = layoutView.FacilityRows;
		if (rows == null || rows.Length == 0)
			return;

		for (int i = 0; i < rows.Length; ++i)
		{
			LabelButtonRowView row = rows[i];
			if (row == null)
				continue;

			bool hasFacility = zone != null && i < zone.OccupiedFacilities.Count;
			row.gameObject.SetActive(hasFacility);
			if (hasFacility == false)
				continue;

			IFacility facility = zone.OccupiedFacilities[i];
			if (facility is not Component component || component == null)
			{
				row.gameObject.SetActive(false);
				continue;
			}

			row.name = component.name + "Row";
			if (row.LabelText != null)
				row.LabelText.text = component.name;
			row.ActionButton?.Configure("View Details", () => HandleViewFacilityDetailsClicked(component.gameObject));
			if (row.ActionButton?.Button != null)
				row.ActionButton.Button.interactable = true;
		}
	}

	private void HandleViewFacilityDetailsClicked(GameObject facilityObject)
	{
		if (facilityObject == null)
			return;

		EnsureSelectionUIMaster();
		selectionUIMaster?.OpenDetailWindow(facilityObject);
	}

	private ZoneArea GetZone()
	{
		return (provider as ZoneUIProvider)?.Target?.Zone;
	}

	private void EnsureSelectionUIMaster()
	{
		if (selectionUIMaster == null)
			selectionUIMaster = GetComponentInParent<SelectionUIMaster>(true);

		if (selectionUIMaster == null)
			selectionUIMaster = FindFirstObjectByType<SelectionUIMaster>(FindObjectsInactive.Include);
	}

	private string BuildFacilitiesPlaceholder(ZoneArea zone)
	{
		if (zone == null || zone.OccupiedFacilities.Count <= 0)
			return "No facilities in this zone.";

		int visibleCapacity = layoutView?.FacilityRows != null ? layoutView.FacilityRows.Length : 0;
		if (visibleCapacity > 0 && zone.OccupiedFacilities.Count > visibleCapacity)
			return $"Showing first {visibleCapacity} of {zone.OccupiedFacilities.Count} facilities.";

		return "Select a facility to inspect details.";
	}

	private static string BuildFacilityListSignature(ZoneArea zone)
	{
		if (zone == null)
			return string.Empty;

		StringBuilder builder = new();
		builder.Append(zone.RuntimeBuildingId);
		builder.Append(':');
		builder.Append(zone.DisplayName);
		builder.Append(':');
		for (int i = 0; i < zone.OccupiedFacilities.Count; ++i)
		{
			IFacility facility = zone.OccupiedFacilities[i];
			if (facility is not Component component || component == null)
				continue;

			if (builder.Length > 0)
				builder.Append('\n');

			builder.Append(component.name);
		}

		return builder.ToString();
	}
}
