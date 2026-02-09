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

	public void SetUpCard(UIProviderBase provider)
	{
		ClearCard();

		iconImage.sprite = provider.Icon;
		titleText.text = provider.Name;
		subTitleText.text = provider.Name;

		SetBody(provider);
	}

	private void SetBody(UIProviderBase provider)
	{
		foreach (var block in provider.InfoBlocks)
		{
			var textObj = selectedTextPool.Get();
			var textMesh = textObj.GetComponent<TextMeshProUGUI>();
			textMesh.text = block.GetContent();
			// Additional setup for textMesh can be done here if needed
		}
	}

}
