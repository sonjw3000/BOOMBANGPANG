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

		CargoPort cargoPort = cargoPortProvider.Target;
		inputReadyValue ??= AddInfoLine("Input Ready");
		interiorAccessValue ??= AddInfoLine("Interior Access");
		exteriorAccessValue ??= AddInfoLine("Exterior Access");
		inputReadyValue.text = cargoPort.CanPutBox() ? "Yes" : "No";
		interiorAccessValue.text = CanUseFromInterior(cargoPort) ? "Open" : "Closed";
		exteriorAccessValue.text = CanUseFromExterior(cargoPort) ? "Open" : "Closed";
	}

	protected override void BuildActionTab()
	{
		base.BuildActionTab();

		var prov = provider as CargoPortUIProvider;
		if (prov?.Target is OutboundCargoPort)
		{
			AddActionButton("Force Load", () =>
			{
				OBService.BuildLoadingTask(prov.Target);
			});
		}
	}

	private static bool CanUseFromInterior(CargoPort cargoPort)
	{
		if (cargoPort == null)
			return false;

		return cargoPort is InboundCargoPort
			? cargoPort.CanGetBox()
			: cargoPort.CanPutBox();
	}

	private static bool CanUseFromExterior(CargoPort cargoPort)
	{
		if (cargoPort == null)
			return false;

		return cargoPort is InboundCargoPort
			? cargoPort.CanPutBox()
			: cargoPort.CanGetBox();
	}
}
