using System.Text;
using System.Collections.Generic;
using TMPro;
using Assets.Scripts.UI;
using UnityEngine;
using UnityEngine.UI;

public class ZoneDetailContent : DetailContent<ZoneSelectionProxy>
{
	[SerializeField] private TextMeshProUGUI nameText;
	[SerializeField] private TextMeshProUGUI typeText;
	[SerializeField] private TextMeshProUGUI boundsText;
	[SerializeField] private TextMeshProUGUI facilitiesHeaderText;
	[SerializeField] private TextMeshProUGUI facilitiesPlaceholderText;
	[SerializeField] private Button extraButton;
	private RectTransform facilitiesListRoot;
	private readonly List<GameObject> facilityRows = new();
	private SelectionUIMaster selectionUIMaster;
	private string lastFacilityListSignature;

	protected override void LinkData()
	{
		EnsureRuntimeFields();
		RebuildFacilityRows();
		UpdateData();

		if (extraButton != null)
			extraButton.gameObject.SetActive(false);
	}

	protected override void UpdateData()
	{
		var zoneProvider = provider as ZoneUIProvider;
		var zone = zoneProvider?.Target?.Zone;
		if (zone == null)
			return;

		EnsureRuntimeFields();
		if (nameText == null)
			return;

		if (typeText == null)
		{
			nameText.text = $"{zone.DisplayName}\n{zone.Type}";
			return;
		}

		nameText.text = zone.DisplayName;
		typeText.text = zone.Type.ToString();
		if (boundsText != null)
		{
			RectInt bounds = zone.Bounds;
			boundsText.text = $"Bounds: {bounds.width}x{bounds.height} @ {bounds.xMin}, {bounds.yMin}  Floor: {zone.Floor}";
		}

		if (facilitiesHeaderText != null)
			facilitiesHeaderText.text = "Facilities";

		if (facilitiesPlaceholderText != null)
			facilitiesPlaceholderText.text = zone.OccupiedFacilities.Count > 0
				? "Select a facility to inspect details."
				: "No facilities in this zone.";

		RebuildFacilityRowsIfNeeded(zone);
	}

	private void EnsureRuntimeFields()
	{
		RectTransform infoRoot = InfoTabRoot;
		if (infoRoot == null)
			return;

		if (nameText == null)
			nameText = CreateRuntimeText("ZoneNameText", infoRoot, 28f);

		if (typeText == null)
		{
			typeText = CreateRuntimeText("ZoneTypeText", infoRoot, 22f);
			typeText.color = new Color(0.8f, 0.86f, 0.94f, 1f);
		}

		if (boundsText == null)
		{
			boundsText = CreateRuntimeText("ZoneBoundsText", infoRoot, 20f);
			boundsText.color = new Color(0.82f, 0.86f, 0.9f, 1f);
		}

		if (facilitiesHeaderText == null)
		{
			facilitiesHeaderText = CreateRuntimeText("FacilitiesHeaderText", infoRoot, 22f);
			facilitiesHeaderText.fontStyle = FontStyles.Bold;
		}

		if (facilitiesListRoot == null)
			facilitiesListRoot = CreateRuntimeVerticalContainer("FacilitiesListRoot", infoRoot, 6f);

		if (facilitiesPlaceholderText == null)
		{
			facilitiesPlaceholderText = CreateRuntimeText("FacilitiesPlaceholderText", infoRoot, 20f);
			facilitiesPlaceholderText.color = new Color(0.82f, 0.86f, 0.9f, 1f);
		}
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
		if (zone == null || facilitiesListRoot == null)
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
		RectTransform row = CreateRuntimeHorizontalContainer(facilityComponent.name + "Row", facilitiesListRoot, 8f);
		facilityRows.Add(row.gameObject);

		TextMeshProUGUI label = CreateRuntimeText(facilityComponent.name + "Label", row, 20f);
		label.text = facilityComponent.name;

		LayoutElement labelLayout = label.GetComponent<LayoutElement>();
		if (labelLayout != null)
		{
			labelLayout.flexibleWidth = 1f;
			labelLayout.minWidth = 0f;
		}

		CreateCompactActionButton(row, "View Details", () => HandleViewFacilityDetailsClicked(facilityComponent.gameObject));
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

	private static TextMeshProUGUI CreateRuntimeText(string objectName, Transform parent, float fontSize)
	{
		GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
		textObject.transform.SetParent(parent, false);

		TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
		text.fontSize = fontSize;
		text.alignment = TextAlignmentOptions.TopLeft;
		text.textWrappingMode = TextWrappingModes.Normal;
		text.color = Color.white;
		return text;
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

	private static RectTransform CreateRuntimeVerticalContainer(string name, Transform parent, float spacing)
	{
		GameObject root = new(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
		root.transform.SetParent(parent, false);

		VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
		layout.spacing = spacing;
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		LayoutElement layoutElement = root.GetComponent<LayoutElement>();
		layoutElement.flexibleWidth = 1f;
		layoutElement.minWidth = 0f;

		ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

		return root.GetComponent<RectTransform>();
	}

	private static RectTransform CreateRuntimeHorizontalContainer(string name, Transform parent, float spacing)
	{
		GameObject root = new(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
		root.transform.SetParent(parent, false);

		HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
		layout.spacing = spacing;
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		LayoutElement layoutElement = root.GetComponent<LayoutElement>();
		layoutElement.flexibleWidth = 1f;
		layoutElement.minWidth = 0f;

		ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

		return root.GetComponent<RectTransform>();
	}

	private static Button CreateCompactActionButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
	{
		GameObject buttonRoot = new(label.Replace(" ", string.Empty) + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
		buttonRoot.transform.SetParent(parent, false);

		LayoutElement layout = buttonRoot.GetComponent<LayoutElement>();
		layout.preferredHeight = 34f;
		layout.minHeight = 34f;
		layout.preferredWidth = 130f;
		layout.minWidth = 130f;
		layout.flexibleWidth = 0f;

		Image image = buttonRoot.GetComponent<Image>();
		image.color = new Color(0.18f, 0.18f, 0.18f, 0.9f);

		Button button = buttonRoot.GetComponent<Button>();
		button.onClick.AddListener(onClick);

		GameObject textRoot = new("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
		textRoot.transform.SetParent(buttonRoot.transform, false);

		TextMeshProUGUI text = textRoot.GetComponent<TextMeshProUGUI>();
		text.text = label;
		text.fontSize = 18f;
		text.alignment = TextAlignmentOptions.Center;
		text.color = Color.white;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.overflowMode = TextOverflowModes.Ellipsis;

		RectTransform textRect = text.rectTransform;
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = Vector2.zero;
		textRect.offsetMax = Vector2.zero;

		return button;
	}
}
