using System.Collections.Generic;
using UnityEngine;

public class SelectionUIMaster : MonoBehaviour
{
	[Header("Select UIs")]
	[SerializeField] private SelectCardUI cardUI = null;
	[SerializeField] private SelectDetailUI detailUI = null;

	[Header("Detail Contents")]
	[SerializeField] private DetailContentBase[] detailContents;

	private readonly Dictionary<System.Type, UIProviderBase> providers = new();

	private UIProviderBase currentProvider = null;
	private DetailContentBase currentDetailContent = null;
	private GameObject currentObj = null;

	private void Awake()
	{
		providers[typeof(Shelf)] = new ShelfUIProvider();
		providers[typeof(BoxPool)] = new BoxPoolUIProvider();
		providers[typeof(RobotWorker)] = new RobotWorkerUIProvider();
		providers[typeof(HumanWorker)] = new HumanWorkerUIProvider();
		providers[typeof(CargoPort)] = new CargoPortUIProvider();
		providers[typeof(ZoneSelectionProxy)] = new ZoneUIProvider();

		GameContext.Instance.InteractionCtx.OnItemSelected += OnSelected;

		cardUI.FocusButton.onClick.AddListener(OnFocusBtnClicked);
		cardUI.DetailsButton.onClick.AddListener(OnDetailClicked);
	}

	private void OnDisable()
	{
		cardUI.DetailsButton.onClick.RemoveListener(OnDetailClicked);
		cardUI.FocusButton.onClick.RemoveListener(OnFocusBtnClicked);

		if (GameContext.HasInstance && GameContext.Instance.InteractionCtx != null)
			GameContext.Instance.InteractionCtx.OnItemSelected -= OnSelected;
	}

	private void Update()
	{
		if (currentProvider != null)
		{
			currentProvider.OnUpdate();
		}
	}

	private void OnSelected(GameObject gridObj)
	{
		currentObj = gridObj;

		GetProvider();
		SelectionChange();
	}

	private bool GetProvider()
	{
		currentProvider = null;

		if (currentObj == null)
			return false;

		foreach (var prov in providers.Values)
		{
			if (prov.IsTargetType(currentObj))
			{
				currentProvider = prov;
				return true;
			}
		}

		Debug.LogWarning($"No suitable UI Provider found for the selected object, Target: {currentObj.name}");
		return false;
	}

	public void SelectionChange()
	{
		if (currentProvider == null)
		{
			DisableCard();
			detailUI.gameObject.SetActive(false);
			return;
		}

		currentProvider.LinkObject(currentObj);
		currentProvider.BuildInfoBlocks();

		cardUI.SetUpCard(currentProvider);
		cardUI.gameObject.SetActive(true);
	}

	private void DisableCard()
	{
		cardUI.ClearCard();
		cardUI.gameObject.SetActive(false);
	}

	public void OnDetailClicked()
	{
		if (currentProvider is ZoneUIProvider zoneProvider)
		{
			detailUI.SetZoneDetail(zoneProvider);
			detailUI.gameObject.SetActive(true);
			return;
		}

		currentDetailContent?.gameObject.SetActive(false);
		currentDetailContent = null;

		foreach (var content in detailContents)
		{
			if (content.IsTargetType(currentObj))
			{
				currentDetailContent = content;
				currentDetailContent.SetProvider(currentProvider);
				break;
			}
		}

		if (currentDetailContent != null)
		{
			detailUI.SetDetailContent(currentDetailContent);
			detailUI.gameObject.SetActive(true);
		}
		else
		{
			Debug.LogWarning($"No suitable UI DetailBuilder found for the selected object, Target: {currentObj.name}");
		}
	}

	public void OnFocusBtnClicked()
	{
	}
}
