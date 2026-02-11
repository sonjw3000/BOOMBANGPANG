using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShelfDetailContent : DetailContent<Shelf>
{
	[SerializeField] private TextMeshProUGUI capacityText;
	[SerializeField] private TextMeshProUGUI currentSizeText;

	protected override void LinkData()
	{
		var prov = (ShelfUIProvider)provider;

		capacityText.text = prov.Capacity.ToString();
		currentSizeText.text = prov.CurrentSize.ToString();
	}

	protected override void UpdateData()
	{
		var prov = (ShelfUIProvider)provider;

		currentSizeText.text = prov.CurrentSize.ToString();
	}
}
