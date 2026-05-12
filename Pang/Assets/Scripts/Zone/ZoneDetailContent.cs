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
		UpdateData();

		if (extraButton != null)
			extraButton.gameObject.SetActive(false);
	}

	protected override void UpdateData()
	{
		var zoneProvider = provider as ZoneUIProvider;
		var zone = zoneProvider?.Target?.Zone;
		if (zone == null || nameText == null)
			return;

		if (typeText == null)
		{
			nameText.text = $"{zone.DisplayName}\n{zone.Type}";
			return;
		}

		nameText.text = zone.DisplayName;
		typeText.text = zone.Type.ToString();
	}
}
