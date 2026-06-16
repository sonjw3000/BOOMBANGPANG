using System.Text;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ZoneDetailContent : DetailContent<ZoneSelectionProxy>
{
	[SerializeField] private ZoneDetailLayoutView layoutPrefab = null;
	[SerializeField] private LabelButtonRowView facilityRowPrefab = null;

	private ZoneDetailLayoutView layoutView;
	private readonly List<GameObject> facilityRows = new();
	private SelectionUIMaster selectionUIMaster;
	private string lastFacilityListSignature;

	protected override void LinkData()
	{
		EnsureLayout();
		RebuildFacilityRows();
		UpdateData();
	}

	protected override void UpdateData()
	{
		var zoneProvider = provider as ZoneUIProvider;
		var zone = zoneProvider?.Target?.Zone;
		if (zone == null)
			return;

		EnsureLayout();
		if (layoutView == null || layoutView.NameText == null)
			return;

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
		{
			layoutView.FacilitiesPlaceholderText.text = zone.OccupiedFacilities.Count > 0
				? "Select a facility to inspect details."
				: "No facilities in this zone.";
		}

		RebuildFacilityRowsIfNeeded(zone);
	}
	private void EnsureLayout()
	{
		if (layoutView != null)
			return;

		if (layoutPrefab == null)
		{
			Debug.LogError("[ZoneDetailContent] Layout prefab is missing.", this);
			return;
		}

		layoutView = Instantiate(layoutPrefab, InfoTabRoot);
		layoutView.name = "ZoneDetailLayout";
		if (layoutView.TypeText != null)
			layoutView.TypeText.color = new Color(0.8f, 0.86f, 0.94f, 1f);
		if (layoutView.BoundsText != null)
			layoutView.BoundsText.color = new Color(0.82f, 0.86f, 0.9f, 1f);
		if (layoutView.FacilitiesPlaceholderText != null)
			layoutView.FacilitiesPlaceholderText.color = new Color(0.82f, 0.86f, 0.9f, 1f);
	}

	private void RebuildFacilityRowsIfNeeded(ZoneArea zone)
	{
		string signature = BuildFacilityListSignature(zone);
		if (lastFacilityListSignature == signature)
			return;

		RebuildFacilityRows();
		lastFacilityListSignature = signature;
	}

	private void RebuildFacilityRows()
	{
		ClearFacilityRows();

		var zoneProvider = provider as ZoneUIProvider;
		ZoneArea zone = zoneProvider?.Target?.Zone;
		if (zone == null || layoutView == null || layoutView.FacilitiesListRoot == null)
			return;

		for (int i = 0; i < zone.OccupiedFacilities.Count; ++i)
		{
			IFacility facility = zone.OccupiedFacilities[i];
			if (facility is not Component component || component == null)
				continue;

			CreateFacilityRow(component);
		}
	}

	private void CreateFacilityRow(Component facilityComponent)
	{
		if (facilityRowPrefab == null || layoutView == null)
		{
			Debug.LogError("[ZoneDetailContent] Facility row prefab is missing.", this);
			return;
		}

		LabelButtonRowView row = Instantiate(facilityRowPrefab, layoutView.FacilitiesListRoot);
		row.name = facilityComponent.name + "Row";
		facilityRows.Add(row.gameObject);

		if (row.LabelText != null)
			row.LabelText.text = facilityComponent.name;

		row.ActionButton?.Configure("View Details", () => HandleViewFacilityDetailsClicked(facilityComponent.gameObject));
	}

	private void ClearFacilityRows()
	{
		for (int i = 0; i < facilityRows.Count; ++i)
		{
			GameObject row = facilityRows[i];
			if (row == null)
				continue;

			row.SetActive(false);
			Destroy(row);
		}

		facilityRows.Clear();
	}

	private void HandleViewFacilityDetailsClicked(GameObject facilityObject)
	{
		if (facilityObject == null)
			return;

		EnsureSelectionUIMaster();
		selectionUIMaster?.OpenDetailWindow(facilityObject);
	}

	private void EnsureSelectionUIMaster()
	{
		if (selectionUIMaster == null)
			selectionUIMaster = GetComponentInParent<SelectionUIMaster>(true);

		if (selectionUIMaster == null)
			selectionUIMaster = FindFirstObjectByType<SelectionUIMaster>(FindObjectsInactive.Include);
	}

	private static string BuildFacilityListSignature(ZoneArea zone)
	{
		if (zone == null || zone.OccupiedFacilities.Count <= 0)
			return string.Empty;

		StringBuilder builder = new();
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
