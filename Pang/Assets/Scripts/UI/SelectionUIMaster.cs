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
		providers[typeof(CargoPort)] = new CargoPortUIProvider();
		providers[typeof(PackingStation)] = new PackingStationUIProvider();
		providers[typeof(Rocket)] = new RocketUIProvider();
		providers[typeof(ShelfBase)] = new ShelfUIProvider();
		providers[typeof(BoxPool)] = new BoxPoolUIProvider();
		providers[typeof(RobotWorker)] = new RobotWorkerUIProvider();
		providers[typeof(HumanWorker)] = new HumanWorkerUIProvider();
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

		currentProvider = GetBestProvider();
		if (currentProvider != null)
			return true;

		Debug.LogWarning($"No suitable UI Provider found for the selected object, Target: {currentObj.name}");
		return false;
	}

	public void SelectionChange()
	{
		if (currentProvider == null || currentObj == null)
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
		if (currentObj == null || currentProvider == null)
		{
			detailUI.gameObject.SetActive(false);
			return;
		}

		if (currentProvider is ZoneUIProvider zoneProvider)
		{
			detailUI.SetZoneDetail(zoneProvider);
			detailUI.gameObject.SetActive(true);
			return;
		}

		currentDetailContent?.gameObject.SetActive(false);
		currentDetailContent = GetBestDetailContent();
		currentDetailContent?.SetProvider(currentProvider);

		if (currentDetailContent != null)
		{
			detailUI.SetDetailContent(currentDetailContent);
			detailUI.gameObject.SetActive(true);
		}
		else
		{
			string targetName = currentObj != null ? currentObj.name : "None";
			Debug.LogWarning($"No suitable UI DetailBuilder found for the selected object, Target: {targetName}");
		}
	}

	public void OnFocusBtnClicked()
	{
	}

	private UIProviderBase GetBestProvider()
	{
		UIProviderBase bestProvider = null;
		int bestDistance = int.MaxValue;

		foreach (UIProviderBase provider in providers.Values)
		{
			int distance = GetMatchDistance(provider.TargetType);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				bestProvider = provider;
			}
		}

		return bestProvider;
	}

	private DetailContentBase GetBestDetailContent()
	{
		DetailContentBase bestContent = null;
		int bestDistance = int.MaxValue;

		foreach (DetailContentBase content in detailContents)
		{
			int distance = GetMatchDistance(content.TargetType);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				bestContent = content;
			}
		}

		return bestContent;
	}

	private int GetMatchDistance(System.Type candidateType)
	{
		if (currentObj == null || candidateType == null)
			return int.MaxValue;

		Component matchedComponent = currentObj.GetComponent(candidateType);
		if (matchedComponent == null)
			return int.MaxValue;

		System.Type currentType = matchedComponent.GetType();
		int distance = 0;
		while (currentType != null)
		{
			if (currentType == candidateType)
				return distance;

			currentType = currentType.BaseType;
			distance++;
		}

		return int.MaxValue;
	}
}
