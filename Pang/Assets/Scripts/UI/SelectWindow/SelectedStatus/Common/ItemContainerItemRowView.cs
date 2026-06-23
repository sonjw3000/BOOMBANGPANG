using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemContainerItemRowView : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI itemNameText;
	[SerializeField] private TextMeshProUGUI quantityText;
	[SerializeField] private TextMeshProUGUI freshnessText;
	[SerializeField] private TextMeshProUGUI damageText;
	[SerializeField] private TextMeshProUGUI orderText;
	private bool built;

	public void Setup(ItemContainerItemDisplayInfo itemInfo)
	{
		EnsureBuilt();

		itemNameText.text = itemInfo?.ItemName ?? "Unknown Item";
		quantityText.text = itemInfo != null ? itemInfo.Quantity.ToString() : "0";
		freshnessText.text = itemInfo != null ? $"{itemInfo.Freshness}%" : "0%";
		damageText.text = itemInfo != null ? $"{itemInfo.Damage}%" : "0%";
		orderText.text = itemInfo?.RelatedOrderId is int orderId ? $"Order #{orderId}" : string.Empty;
		orderText.gameObject.SetActive(true);
		itemNameText.fontStyle = FontStyles.Normal;
		quantityText.fontStyle = FontStyles.Normal;
		freshnessText.fontStyle = FontStyles.Normal;
		damageText.fontStyle = FontStyles.Normal;
		orderText.fontStyle = FontStyles.Normal;
	}

	public void SetupHeader()
	{
		EnsureBuilt();
		itemNameText.text = "Item";
		quantityText.text = "Quantity";
		freshnessText.text = "Fresh";
		damageText.text = "Damage";
		orderText.text = "Related Order";
		orderText.gameObject.SetActive(true);
		itemNameText.fontStyle = FontStyles.Bold;
		quantityText.fontStyle = FontStyles.Bold;
		freshnessText.fontStyle = FontStyles.Bold;
		damageText.fontStyle = FontStyles.Bold;
		orderText.fontStyle = FontStyles.Bold;
	}

	private void EnsureBuilt()
	{
		if (built)
			return;

		TryBindFromExistingChildren();

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

		itemNameText ??= CreateText("ItemName", transform, 18f, TextAlignmentOptions.Left);
		quantityText ??= CreateText("Quantity", transform, 18f, TextAlignmentOptions.Center);
		freshnessText ??= CreateText("Freshness", transform, 18f, TextAlignmentOptions.Center);
		damageText ??= CreateText("Damage", transform, 18f, TextAlignmentOptions.Center);
		orderText ??= CreateText("Order", transform, 18f, TextAlignmentOptions.Right);

		itemNameText.transform.SetSiblingIndex(0);
		quantityText.transform.SetSiblingIndex(1);
		freshnessText.transform.SetSiblingIndex(2);
		damageText.transform.SetSiblingIndex(3);
		orderText.transform.SetSiblingIndex(4);

		SetTextLayout(itemNameText, 0f, 1f, 140f);
		SetTextLayout(quantityText, 84f, 0f, 84f);
		SetTextLayout(freshnessText, 72f, 0f, 72f);
		SetTextLayout(damageText, 84f, 0f, 84f);
		SetTextLayout(orderText, 150f, 0f, 150f);

		built = true;
	}

	private void TryBindFromExistingChildren()
	{
		if (itemNameText == null)
			itemNameText = transform.Find("ItemName")?.GetComponent<TextMeshProUGUI>();

		if (quantityText == null)
			quantityText = transform.Find("Quantity")?.GetComponent<TextMeshProUGUI>();

		if (freshnessText == null)
			freshnessText = transform.Find("Freshness")?.GetComponent<TextMeshProUGUI>();

		if (damageText == null)
			damageText = transform.Find("Damage")?.GetComponent<TextMeshProUGUI>();

		if (orderText == null)
			orderText = transform.Find("Order")?.GetComponent<TextMeshProUGUI>();
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
