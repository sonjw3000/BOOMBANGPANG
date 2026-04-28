using UnityEngine;
using UnityEngine.UI;

public class CargoPortDetailBuilder : DetailContent<CargoPort>
{
	[SerializeField] private Button forceLoadButton;

	static private OutboundWorkflowManager OBManager => GameContext.Instance.OBWorkflowMgr;


	protected override void AddListener()
	{
		var prov = (CargoPortUIProvider)provider;

		forceLoadButton.gameObject.SetActive(false);

		if (prov.Target.IsInbound == false)
		{
			forceLoadButton.gameObject.SetActive(true);
			forceLoadButton.onClick.AddListener(() =>
			{
				OBManager.BuildLoadingTask(prov.Target);
			});
		}
	}

	protected override void RemoveListeners()
	{
		forceLoadButton.onClick.RemoveAllListeners();
	}

	protected override void LinkData()
	{
		var prov = (CargoPortUIProvider)provider;
	}
}
