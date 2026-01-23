using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class SelectCardUI : MonoBehaviour
{
	[Header("UI References")]
	[SerializeField] private Image iconImage = null;
	[SerializeField] private TextMeshProUGUI titleText = null;
	[SerializeField] private TextMeshProUGUI subTitleText = null;
	[SerializeField] private Transform bodyTextTransform = null;
	[SerializeField] private GameObject bodyTextExample = null;

	[Header("Buttons")]
	[SerializeField] public Button FocusButton = null;
	[SerializeField] public Button DetailsButton = null;

	[Header("Text ItemPool")]
	private readonly int textPoolSize = 3;

	private GameObjectPool selectedTextPool;

	private void Start()
	{
		selectedTextPool = new(textPoolSize, () => { return Instantiate(bodyTextExample, bodyTextTransform); });

		gameObject.SetActive(false);
	}

	public void ClearCard()
	{
		selectedTextPool.ReleaseAll();
	}

	public void SetUpCard(SelectionModel selectionModel)
	{
		ClearCard();

		iconImage.sprite = selectionModel.icon;
		titleText.text = selectionModel.title;
		subTitleText.text = selectionModel.subtitle;

		SetBody(selectionModel);
	}

	public void SetBody(SelectionModel selectionModel)
	{
		foreach (var block in selectionModel.blocks)
		{
			var textObj = selectedTextPool.Get();
			var textMesh = textObj.GetComponent<TextMeshProUGUI>();
			//textMesh.text = block.content;
			// Additional setup for textMesh can be done here if needed
		}
	}

}
