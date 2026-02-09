using Assets.Scripts.AI.BT;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class SelectionUIMaster : MonoBehaviour
{
	[Header("Select UIs")]
	[SerializeField] private SelectCardUI cardUI = null;
	[SerializeField] private SelectDetailUI detailUI = null;

	[Header("Detail Contents")]
	[SerializeField] private DetailContentBase[] detailContents;

	private readonly List<UIProviderBase> providers = new();

	private UIProviderBase currentProvider = null;
	private DetailContentBase currentDetailContent = null;
	private GameObject currentObj = null;


	private void Awake()
	{
		var asm = typeof(UIProviderBase).Assembly;
		var providerTypes =
			from type in asm.GetTypes()
			where type.IsAbstract == false
			where typeof(UIProviderBase).IsAssignableFrom(type)
			select type;

		foreach (var type in providerTypes)
		{
			var provider = (UIProviderBase)System.Activator.CreateInstance(type);
			providers.Add(provider);
		}
	}

	private void Start()
	{
		GameContext.Instance.InteractionCtx.OnItemSelected += OnSelected;

		cardUI.FocusButton.onClick.AddListener(OnFocusBtnClicked);
		cardUI.DetailsButton.onClick.AddListener(OnDetailClicked);
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

		foreach (var provider in providers)
		{
			if (provider.IsTargetType(currentObj))
			{
				currentProvider = provider;
				return true;
			}
		}

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
