using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemContainerItemRowView : MonoBehaviour
{
	private const float ItemColumnWidth = 0f;
	private const float QuantityColumnWidth = 84f;
	private const float OrderColumnWidth = 180f;

	private TextMeshProUGUI itemNameText;
	private TextMeshProUGUI quantityText;
	private TextMeshProUGUI orderText;
	private bool built;

	public void Setup(ItemContainerItemDisplayInfo itemInfo)
	{
		EnsureBuilt();

		itemNameText.text = itemInfo?.ItemName ?? "Unknown Item";
		quantityText.text = itemInfo != null ? itemInfo.Quantity.ToString() : "0";
		orderText.text = itemInfo?.RelatedOrderId is int orderId ? $"Order #{orderId}" : string.Empty;
		orderText.gameObject.SetActive(itemInfo?.RelatedOrderId != null);
	}

	public void SetupHeader()
	{
		EnsureBuilt();
		itemNameText.text = "Item";
		quantityText.text = "Quantity";
		orderText.text = "Related Order";
		orderText.gameObject.SetActive(true);
		itemNameText.fontStyle = FontStyles.Bold;
		quantityText.fontStyle = FontStyles.Bold;
		orderText.fontStyle = FontStyles.Bold;
	}

	private void EnsureBuilt()
	{
		if (built)
			return;

		RectTransform rect = gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.sizeDelta = new Vector2(0f, 0f);

		HorizontalLayoutGroup layout = gameObject.GetComponent<HorizontalLayoutGroup>() ?? gameObject.AddComponent<HorizontalLayoutGroup>();
		layout.spacing = 8f;
		layout.padding = new RectOffset(8, 8, 4, 4);
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		ContentSizeFitter fitter = gameObject.GetComponent<ContentSizeFitter>() ?? gameObject.AddComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		LayoutElement layoutElement = gameObject.GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
		layoutElement.flexibleWidth = 1f;
		layoutElement.minWidth = 0f;
		layoutElement.minHeight = 34f;

		itemNameText = CreateText("ItemName", transform, 18f, TextAlignmentOptions.Left);
		SetTextLayout(itemNameText, ItemColumnWidth, 1f, 160f);

		quantityText = CreateText("Quantity", transform, 18f, TextAlignmentOptions.Center);
		SetTextLayout(quantityText, QuantityColumnWidth, 0f, QuantityColumnWidth);

		orderText = CreateText("Order", transform, 18f, TextAlignmentOptions.Right);
		SetTextLayout(orderText, OrderColumnWidth, 0f, OrderColumnWidth);

		built = true;
	}

	private static void SetTextLayout(TextMeshProUGUI text, float preferredWidth, float flexibleWidth, float minWidth)
	{
		LayoutElement layout = text.GetComponent<LayoutElement>() ?? text.gameObject.AddComponent<LayoutElement>();
		layout.preferredWidth = preferredWidth;
		layout.flexibleWidth = flexibleWidth;
		layout.minWidth = minWidth;
	}

	private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, TextAlignmentOptions alignment)
	{
		GameObject textRoot = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
		textRoot.transform.SetParent(parent, false);

		TextMeshProUGUI text = textRoot.GetComponent<TextMeshProUGUI>();
		text.fontSize = fontSize;
		text.color = Color.white;
		text.alignment = alignment;
		text.textWrappingMode = TextWrappingModes.Normal;
		text.overflowMode = TextOverflowModes.Overflow;
		return text;
	}
}
