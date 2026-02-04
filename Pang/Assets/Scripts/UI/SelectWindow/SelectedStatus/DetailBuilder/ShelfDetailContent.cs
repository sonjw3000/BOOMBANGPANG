using UnityEngine;
using TMPro;

public class ShelfDetailContent : DetailContent<ShelfUIProvider>
{
	[SerializeField] private TextMeshProUGUI capacityText;
	[SerializeField] private TextMeshProUGUI currentSizeText;

	protected override void LinkData()
	{
		capacityText.text = provider.Capacity.ToString();
		currentSizeText.text = provider.CurrentSize.ToString();
	}

	private void Update()
	{
		if (provider != null)
		{
			currentSizeText.text = provider.CurrentSize.ToString();
		}
	}
}
