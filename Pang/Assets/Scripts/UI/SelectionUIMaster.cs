using UnityEngine;

public class SelectionUIMaster : MonoBehaviour
{
	//[SerializeField] private GameContext gameContext;

	private IGridPlaceable currentObj = null;


	private void Start()
	{
		// interaction에서 선택된 아이템이 변경되었을 때
		GameContext.Instance.InteractionCtx.OnItemSelected += OnSelected;
	}

	private void OnSelected(IGridPlaceable gridObj)
	{
		currentObj = gridObj;
		if (currentObj == null)
		{
			// off selectionCard

			return;
		}



	}

}
