using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BoxPoolDetailContent : DetailContent<BoxPool>
{
	[SerializeField] private Button addBoxButton;

	[SerializeField] private TextMeshProUGUI boxCapacityText;
	[SerializeField] private TextMeshProUGUI currentBoxText;

	private static BoxPoolService BoxPoolService => GameContext.Instance.WMSys.BoxPoolMgr;

	protected override void BuildActionButtons(RectTransform actionRoot)
	{
		base.BuildActionButtons(actionRoot);
		addBoxButton?.gameObject.SetActive(false);
		RegisterActionButton(CreateRuntimeActionButton(actionRoot, "Add Personal Box", () =>
		{
			BoxPoolService.GiveNewBox(((BoxPoolUIProvider)provider).Target, BoxType.Personal);
		}));
	}

	protected override void LinkData()
	{
		var prov = (BoxPoolUIProvider)provider;
		addBoxButton?.gameObject.SetActive(false);
		deleteButton?.gameObject.SetActive(false);

		currentBoxText.text = prov.CurrentBoxCount.ToString();
	}

	protected override void UpdateData()
	{
		var prov = (BoxPoolUIProvider)provider;

		currentBoxText.text = prov.CurrentBoxCount.ToString();
	}
}
