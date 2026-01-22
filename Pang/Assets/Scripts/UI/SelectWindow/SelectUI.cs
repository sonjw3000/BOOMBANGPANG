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

		if (selectionModel == null)
		{
			// disalbe
			DisalbeWindow();
			return;
		}
		itemImage.sprite = selectionModel.icon;
		itemTitle.text = selectionModel.title;

		BuildContent();
		EnableWindow();
	}

	private void DisalbeWindow()
	{

		gameObject.SetActive(false);
	}

	private void EnableWindow()
	{
		gameObject.SetActive(true);
	}


	private void BuildContent()
	{
		// todo
		// build ui here by SelectionModel
	}

}
