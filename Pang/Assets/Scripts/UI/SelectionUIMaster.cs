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

		GameContext.Instance.InteractionCtx.OnItemSelected += OnSelected;

		cardUI.FocusButton.onClick.AddListener(OnFocusBtnClicked);
		cardUI.DetailsButton.onClick.AddListener(OnDetailClicked);
	}

	private void Start()
	{

	}

	private void OnDisable()
	{
		//cardUI.DetailsButton.onClick.RemoveListener(OnDetailClicked);
		//cardUI.FocusButton.onClick.RemoveListener(OnFocusBtnClicked);

		//GameContext.Instance.InteractionCtx.OnItemSelected -= OnSelected;
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
		{
			//Debug.LogError("Current Object is null");
			return false;
		}

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
			// disalbe
			DisableCard();
			detailUI.gameObject.SetActive(false);
			return;
		}

		currentProvider.LinkObject(currentObj);
		currentProvider.BuildInfoBlocks();

		// enable card UI
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
		// 여기서 각 UIProvider에 맞는 DetailUI를 활성화 시켜줘야함
		currentDetailContent?.gameObject.SetActive(false);

		foreach (var content in detailContents)
		{
			if (content.IsTargetType(currentObj))
			{
				currentDetailContent = content;
				currentDetailContent.SetProvider(currentProvider);
				break;
			}
		}

		detailUI.SetDetailContent(currentDetailContent);
		detailUI.gameObject.SetActive(true);
	}

	public void OnFocusBtnClicked()
	{

	}


}
