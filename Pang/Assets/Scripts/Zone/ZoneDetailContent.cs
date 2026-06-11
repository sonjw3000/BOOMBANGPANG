using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ZoneDetailContent : DetailContent<ZoneSelectionProxy>
{
	[SerializeField] private TextMeshProUGUI nameText;
	[SerializeField] private TextMeshProUGUI typeText;
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
	}

	private void EnsureRuntimeFields()
	{
		if (nameText != null)
			return;

		RectTransform infoRoot = InfoTabRoot;
		if (infoRoot == null)
			return;

		nameText = CreateRuntimeText("ZoneNameText", infoRoot, 28f);
		typeText = CreateRuntimeText("ZoneTypeText", infoRoot, 22f);
		typeText.color = new Color(0.8f, 0.86f, 0.94f, 1f);
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
}
