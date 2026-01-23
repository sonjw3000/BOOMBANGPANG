using UnityEngine;

public class SelectionUIMaster : MonoBehaviour
{
	//[SerializeField] private GameContext gameContext;

	private GameObject currentObj = null;

	private SelectionModel selectionModel = null;

	[SerializeField] private SelectCardUI cardUI = null;
	[SerializeField] private SelectDetailUI detailUI = null;

	private void Start()
	{
		GameContext.Instance.InteractionCtx.OnItemSelected += OnSelected;

		cardUI.FocusButton.onClick.AddListener(OnFocusBtnClicked);
		cardUI.DetailsButton.onClick.AddListener(OnDetailClicked);
	}

	private void OnSelected(GameObject gridObj)
	{
		currentObj = gridObj;

		TryBuildModel(currentObj, out SelectionModel model);
		SelectionChange(model);
	}

	private bool TryBuildModel(GameObject obj, out SelectionModel model)
	{
		model = null;

		if (obj == null)
			return false;
		
		if (obj.TryGetComponent<UIProviderBase>(out var placeable) == false)
		{
			return false;
		}

		return placeable.TryBuild(out model);
	}

	public void SelectionChange(SelectionModel selectionModel)
	{
		this.selectionModel = selectionModel;

		if (selectionModel == null)
		{
			// disalbe
			DisableCard();
			detailUI.gameObject.SetActive(false);
			return;
		}

		// enable card UI
		cardUI.SetUpCard(selectionModel);
		cardUI.gameObject.SetActive(true);
	}

	private void DisableCard()
	{
		cardUI.ClearCard();
		cardUI.gameObject.SetActive(false);
	}

	public void OnDetailClicked()
	{
		//detailUI.SetUpDetail(selectionModel);
		//detailUI.gameObject.SetActive(true);
	}

	public void OnFocusBtnClicked()
	{

	}


}
