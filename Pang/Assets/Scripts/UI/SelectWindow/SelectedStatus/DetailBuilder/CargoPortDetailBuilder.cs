using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CargoPortDetailBuilder : ShelfBaseDetailContent<CargoPort>
{
	[SerializeField] private Button forceLoadButton;
	private TextMeshProUGUI inputReadyValue;

	static private OutboundWorkflowService OBService => GameContext.Instance.OBWorkflowSvc;

	protected override void LinkData()
	{
		forceLoadButton?.gameObject.SetActive(false);
		deleteButton?.gameObject.SetActive(false);
		base.LinkData();
	}

	protected override void RefreshExtraInfo(IShelfBaseUIProvider shelfProvider)
	{
		if (provider is not CargoPortUIProvider cargoPortProvider || cargoPortProvider.Target == null)
			return;

		inputReadyValue ??= AddInfoLine("Input Ready");
		inputReadyValue.text = cargoPortProvider.Target.InputReady ? "Yes" : "No";
	}

	protected override void BuildActionTab()
	{
		base.BuildActionTab();

		var prov = provider as CargoPortUIProvider;
		if (prov?.Target != null && prov.Target.IsInbound == false)
		{
			AddActionButton("Force Load", () =>
			{
				OBService.BuildLoadingTask(prov.Target);
			});
		}
	}
}
