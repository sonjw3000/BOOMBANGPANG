using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ManifestContainerItemRowView : MonoBehaviour
{
	[SerializeField] private TextMeshProUGUI orderIdText;
	[SerializeField] private TextMeshProUGUI itemNameText;
	[SerializeField] private TextMeshProUGUI inBoxText;
	[SerializeField] private TextMeshProUGUI orderProgressText;
	[SerializeField] private TextMeshProUGUI weeksLeftText;
	private bool built;

	public void Setup(ManifestContainerItemDisplayInfo itemInfo)
	{
		EnsureBuilt();

		orderIdText.text = itemInfo?.OrderId ?? "#0";
		itemNameText.text = itemInfo?.ItemName ?? "Unknown Item";
		inBoxText.text = itemInfo?.InBoxQuantity ?? "0 picked";
		orderProgressText.text = itemInfo?.OrderProgress ?? "0 / 0";
		weeksLeftText.text = itemInfo?.WeeksLeft ?? "0";
		SetFontStyle(FontStyles.Normal);
	}

	public void SetupHeader()
	{
		EnsureBuilt();

		orderIdText.text = "Order ID";
		itemNameText.text = "Item";
		inBoxText.text = "In Box";
		orderProgressText.text = "Order Progress";
		weeksLeftText.text = "WeeksLeft";
		SetFontStyle(FontStyles.Bold);
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

		orderIdText ??= CreateText("OrderId", transform, 16f, TextAlignmentOptions.Left);
		itemNameText ??= CreateText("ItemName", transform, 16f, TextAlignmentOptions.Left);
		inBoxText ??= CreateText("InBox", transform, 16f, TextAlignmentOptions.Center);
		orderProgressText ??= CreateText("OrderProgress", transform, 16f, TextAlignmentOptions.Left);
		weeksLeftText ??= CreateText("WeeksLeft", transform, 16f, TextAlignmentOptions.Center);

		orderIdText.transform.SetSiblingIndex(0);
		itemNameText.transform.SetSiblingIndex(1);
		inBoxText.transform.SetSiblingIndex(2);
		orderProgressText.transform.SetSiblingIndex(3);
		weeksLeftText.transform.SetSiblingIndex(4);

		SetTextLayout(orderIdText, 72f, 0f, 72f);
		SetTextLayout(itemNameText, 0f, 1f, 110f);
		SetTextLayout(inBoxText, 88f, 0f, 88f);
		SetTextLayout(orderProgressText, 150f, 0f, 150f);
		SetTextLayout(weeksLeftText, 72f, 0f, 72f);

		built = true;
	}

	private void TryBindFromExistingChildren()
	{
		orderIdText ??= transform.Find("OrderId")?.GetComponent<TextMeshProUGUI>();
		itemNameText ??= transform.Find("ItemName")?.GetComponent<TextMeshProUGUI>();
		inBoxText ??= transform.Find("InBox")?.GetComponent<TextMeshProUGUI>();
		orderProgressText ??= transform.Find("OrderProgress")?.GetComponent<TextMeshProUGUI>();
		weeksLeftText ??= transform.Find("WeeksLeft")?.GetComponent<TextMeshProUGUI>();
	}

	private void SetFontStyle(FontStyles style)
	{
		orderIdText.fontStyle = style;
		itemNameText.fontStyle = style;
		inBoxText.fontStyle = style;
		orderProgressText.fontStyle = style;
		weeksLeftText.fontStyle = style;
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
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.overflowMode = TextOverflowModes.Ellipsis;
		return text;
	}
}
