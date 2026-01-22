using UnityEngine;
using UnityEngine.UI;


public class SelectUI : MonoBehaviour
{
	private SelectionModel selectionModel = null;
	//private Wind

	[SerializeField] private Image itemImage = null;
	[SerializeField] private Text itemTitle = null;

	public void OnSelected(SelectionModel selectionModel)
	{
		this.selectionModel = selectionModel;

		// todo
		// build ui here
		if (selectionModel == null)
		{
			// disalbe
			DisalbeWindow();
			return;
		}

		itemImage.sprite = selectionModel.icon;
		itemTitle.text = selectionModel.title;
	}

	private void DisalbeWindow()
	{

		gameObject.SetActive(false);
	}

}
