using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemContainerPanelView : MonoBehaviour
{
	private const string RowPrefabPath = "UI/Select/DetailContents/ItemContainerItemRowView";

	[SerializeField] private TextMeshProUGUI boxNameText;
	[SerializeField] private TextMeshProUGUI emptyStateText;
	[SerializeField] private ItemContainerItemRowView headerRow;
	[SerializeField] private RectTransform contentRoot;
	private GameObjectPool rowPool;
	private bool built;

	public void SetView(ItemContainerDisplayInfo boxInfo)
	{
		EnsureBuilt();
		rowPool?.ReleaseAll();

		boxNameText.text = boxInfo?.HasContainer == true ? boxInfo.ContainerName : "None";

		if (boxInfo?.HasContainer != true)
		{
			emptyStateText.text = "No container";
			emptyStateText.gameObject.SetActive(true);
			return;
		}

		if (boxInfo.Items == null || boxInfo.Items.Count == 0)
		{
			emptyStateText.text = "Empty";
			emptyStateText.gameObject.SetActive(true);
			return;
		}

		emptyStateText.gameObject.SetActive(false);
		foreach (ItemContainerItemDisplayInfo item in boxInfo.Items)
		{
			ItemContainerItemRowView row = rowPool.Get().GetComponent<ItemContainerItemRowView>();
			row.transform.SetParent(contentRoot, false);
			row.Setup(item);
		}
	}

	private void EnsureBuilt()
	{
		if (built)
			return;

		TryBindFromExistingChildren();

		if (boxNameText != null && emptyStateText != null && headerRow != null && contentRoot != null)
		{
			headerRow.SetupHeader();
			rowPool = new GameObjectPool(6, CreateRowObject);
			built = true;
			return;
		}

		RectTransform rect = gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.sizeDelta = new Vector2(0f, 0f);

		Image background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
		background.color = new Color(0.22f, 0.22f, 0.22f, 0.9f);

		VerticalLayoutGroup layout = gameObject.GetComponent<VerticalLayoutGroup>() ?? gameObject.AddComponent<VerticalLayoutGroup>();
		layout.spacing = 6f;
		layout.padding = new RectOffset(8, 8, 8, 8);
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		ContentSizeFitter fitter = gameObject.GetComponent<ContentSizeFitter>() ?? gameObject.AddComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		LayoutElement rootLayout = gameObject.GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
		rootLayout.flexibleWidth = 1f;
		rootLayout.minWidth = 0f;

		boxNameText = CreateText("ContainerName", transform, 20f, FontStyles.Bold);

		GameObject scrollRoot = new("ScrollRoot", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect), typeof(LayoutElement));
		scrollRoot.transform.SetParent(transform, false);

		Image scrollImage = scrollRoot.GetComponent<Image>();
		scrollImage.color = new Color(0f, 0f, 0f, 0.18f);

		Mask mask = scrollRoot.GetComponent<Mask>();
		mask.showMaskGraphic = false;

		ScrollRect scrollRect = scrollRoot.GetComponent<ScrollRect>();
		scrollRect.horizontal = false;
		scrollRect.vertical = true;
		scrollRect.scrollSensitivity = 24f;

		LayoutElement scrollLayout = scrollRoot.GetComponent<LayoutElement>();
		scrollLayout.preferredHeight = 160f;
		scrollLayout.minHeight = 160f;
		scrollLayout.flexibleWidth = 1f;

		RectTransform scrollRectTransform = scrollRoot.GetComponent<RectTransform>();
		scrollRectTransform.anchorMin = new Vector2(0f, 1f);
		scrollRectTransform.anchorMax = new Vector2(1f, 1f);
		scrollRectTransform.pivot = new Vector2(0.5f, 1f);

		GameObject content = new("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
		content.transform.SetParent(scrollRoot.transform, false);
		contentRoot = content.GetComponent<RectTransform>();
		contentRoot.anchorMin = new Vector2(0f, 1f);
		contentRoot.anchorMax = new Vector2(1f, 1f);
		contentRoot.pivot = new Vector2(0.5f, 1f);
		contentRoot.anchoredPosition = Vector2.zero;
		contentRoot.sizeDelta = Vector2.zero;

		VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
		contentLayout.spacing = 4f;
		contentLayout.padding = new RectOffset(4, 4, 4, 4);
		contentLayout.childAlignment = TextAnchor.UpperLeft;
		contentLayout.childControlWidth = true;
		contentLayout.childControlHeight = true;
		contentLayout.childForceExpandWidth = true;
		contentLayout.childForceExpandHeight = false;

		ContentSizeFitter contentFitter = content.GetComponent<ContentSizeFitter>();
		contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		scrollRect.viewport = scrollRoot.GetComponent<RectTransform>();
		scrollRect.content = contentRoot;

		headerRow = CreateHeaderRow();
		headerRow.SetupHeader();

		emptyStateText = CreateText("EmptyState", contentRoot, 18f, FontStyles.Normal);
		emptyStateText.text = "No container";

		rowPool = new GameObjectPool(6, CreateRowObject);
		built = true;
	}

	private void TryBindFromExistingChildren()
	{
		if (boxNameText == null)
			boxNameText = transform.Find("ContainerName")?.GetComponent<TextMeshProUGUI>();

		Transform scrollRoot = transform.Find("ScrollRoot");
		if (contentRoot == null)
			contentRoot = scrollRoot?.Find("Content") as RectTransform;

		if (contentRoot != null)
		{
			if (headerRow == null)
				headerRow = contentRoot.Find("HeaderRow")?.GetComponent<ItemContainerItemRowView>();

			if (emptyStateText == null)
				emptyStateText = contentRoot.Find("EmptyState")?.GetComponent<TextMeshProUGUI>();
		}
	}

	private GameObject CreateRowObject()
	{
		ItemContainerItemRowView rowPrefab = Resources.Load<ItemContainerItemRowView>(RowPrefabPath);
		if (rowPrefab != null)
			return Instantiate(rowPrefab.gameObject, contentRoot);

		GameObject rowObject = new("ItemContainerItemRow", typeof(RectTransform), typeof(ItemContainerItemRowView));
		rowObject.transform.SetParent(contentRoot, false);
		return rowObject;
	}

	private ItemContainerItemRowView CreateHeaderRow()
	{
		ItemContainerItemRowView rowPrefab = Resources.Load<ItemContainerItemRowView>(RowPrefabPath);
		if (rowPrefab != null)
			return Instantiate(rowPrefab, contentRoot);

		GameObject rowObject = new("HeaderRow", typeof(RectTransform), typeof(ItemContainerItemRowView));
		rowObject.transform.SetParent(contentRoot, false);
		return rowObject.GetComponent<ItemContainerItemRowView>();
	}

	private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles fontStyle)
	{
		GameObject textRoot = new(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
		textRoot.transform.SetParent(parent, false);

		TextMeshProUGUI text = textRoot.GetComponent<TextMeshProUGUI>();
		text.fontSize = fontSize;
		text.fontStyle = fontStyle;
		text.color = Color.white;
		text.alignment = TextAlignmentOptions.TopLeft;
		text.textWrappingMode = TextWrappingModes.Normal;
		text.overflowMode = TextOverflowModes.Overflow;

		LayoutElement layout = textRoot.GetComponent<LayoutElement>();
		layout.flexibleWidth = 1f;
		layout.minWidth = 0f;
		return text;
	}
}
