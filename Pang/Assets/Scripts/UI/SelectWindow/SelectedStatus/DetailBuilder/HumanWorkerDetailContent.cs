using TMPro;
using UnityEngine;

public class HumanWorkerDetailContent : DetailContent<HumanWorker>
{
	[SerializeField] private TextMeshProUGUI fatigue;

	protected override void LinkData()
	{
		var prov = (HumanWorkerUIProvider)provider;

		fatigue.text = prov.Fatigue.ToString();
	}

	protected override void UpdateData()
	{
		var prov = (HumanWorkerUIProvider)provider;

		fatigue.text = prov.Fatigue.ToString();
	}
}
