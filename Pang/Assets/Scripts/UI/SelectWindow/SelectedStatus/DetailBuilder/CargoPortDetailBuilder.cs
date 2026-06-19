using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CargoPortDetailBuilder : ShelfBaseDetailContent<CargoPort>
{
	[SerializeField] private Button forceLoadButton;
	private TextMeshProUGUI inputReadyValue;
	private TextMeshProUGUI interiorAccessValue;
	private TextMeshProUGUI exteriorAccessValue;

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
		interiorAccessValue ??= AddInfoLine("Interior Access");
		exteriorAccessValue ??= AddInfoLine("Exterior Access");
		inputReadyValue.text = cargoPortProvider.Target.InputReady ? "Yes" : "No";
		interiorAccessValue.text = cargoPortProvider.Target.CanUseFromInterior ? "Open" : "Closed";
		exteriorAccessValue.text = cargoPortProvider.Target.CanUseFromExterior ? "Open" : "Closed";
	}

	protected override void BuildActionTab()
	{
		base.BuildActionTab();

		var prov = provider as CargoPortUIProvider;
		if (prov?.Target != null && prov.Target.IsOutbound)
		{
			AddActionButton("Force Load", () =>
			{
				OBService.BuildLoadingTask(prov.Target);
			});
		}
	}
}
