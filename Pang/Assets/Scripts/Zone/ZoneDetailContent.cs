using TMPro;
using System.Text;
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

	protected override void LinkData()
	{
		EnsureRuntimeFields();
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
			facilitiesPlaceholderText.text = BuildFacilityListText(zone);
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

		if (facilitiesPlaceholderText == null)
		{
			facilitiesPlaceholderText = CreateRuntimeText("FacilitiesPlaceholderText", infoRoot, 20f);
			facilitiesPlaceholderText.color = new Color(0.82f, 0.86f, 0.9f, 1f);
		}
	}

	private static TextMeshProUGUI CreateRuntimeText(string objectName, Transform parent, float fontSize)
	{
		GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
		textObject.transform.SetParent(parent, false);

		TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
		text.fontSize = fontSize;
		text.alignment = TextAlignmentOptions.TopLeft;
		text.textWrappingMode = TextWrappingModes.Normal;
		text.color = Color.white;
		return text;
	}

	private static string BuildFacilityListText(ZoneArea zone)
	{
		if (zone == null || zone.OccupiedFacilities.Count <= 0)
			return "No facilities in this zone.";

		StringBuilder builder = new();
		for (int i = 0; i < zone.OccupiedFacilities.Count; ++i)
		{
			IFacility facility = zone.OccupiedFacilities[i];
			if (facility is not Component component || component == null)
				continue;

			if (builder.Length > 0)
				builder.Append('\n');

			builder.Append("- ");
			builder.Append(component.name);
		}

		return builder.Length > 0 ? builder.ToString() : "No facilities in this zone.";
	}
}
