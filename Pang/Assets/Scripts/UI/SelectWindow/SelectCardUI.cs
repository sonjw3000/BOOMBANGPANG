using System.Collections.Generic;
using TMPro;
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

	private Dictionary<InfoBlockType, GameObjectPool> infoPools = new();

	private void Awake()
	{
		if (bodyTextExample != null)
		{
			infoPools[InfoBlockType.KeyValue] = new(textPoolSize, () => { return Instantiate(bodyTextExample, bodyTextTransform); });
		}
	}

	private void Start()
	{
		gameObject.SetActive(false);
	}

	public void ClearCard()
	{
		foreach (var pool in infoPools.Values)
		{
			pool.ReleaseAll();
		}
	}

	public void SetUpCard(UIProviderBase provider)
	{
		ClearCard();

		iconImage.sprite = provider.Icon;
		titleText.text = provider.Name;
		subTitleText.text = provider.Subtitle;

		SetBody(provider);
	}

	private void SetBody(UIProviderBase provider)
	{
		foreach (var block in provider.InfoBlocks)
		{
			if (infoPools.TryGetValue(block.InfoType, out var pool))
			{
				var textObj = pool.Get();
				block.SetGameObject(textObj);
			}
			else
			{
				Debug.LogWarning($"No pool found for InfoBlockType: {block.InfoType}");
			}
		}
	}

}
