using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemContainerPanelView : MonoBehaviour
{
	private const string RowPrefabPath = "UI/Select/DetailContents/ItemContainerItemRowView";
	private const string ManifestRowPrefabPath = "UI/Select/DetailContents/ManifestContainerItemRowView";

	[SerializeField] private TextMeshProUGUI boxNameText;
	[SerializeField] private TextMeshProUGUI emptyStateText;
	[SerializeField] private TextMeshProUGUI itemsSectionTitleText;
	[SerializeField] private TextMeshProUGUI manifestSectionTitleText;
	[SerializeField] private ItemContainerItemRowView headerRow;
	[SerializeField] private ManifestContainerItemRowView manifestHeaderRow;
	[SerializeField] private RectTransform contentRoot;
	[SerializeField] private RectTransform itemRowsRoot;
	[SerializeField] private RectTransform manifestRowsRoot;
	private GameObjectPool rowPool;
	private GameObjectPool manifestRowPool;
	private bool built;

	public void SetView(ItemContainerDisplayInfo boxInfo)
	{
		EnsureBuilt();
		rowPool?.ReleaseAll();
		manifestRowPool?.ReleaseAll();

		boxNameText.text = boxInfo?.HasContainer == true ? boxInfo.ContainerName : "None";

		if (boxInfo?.HasContainer != true)
		{
			emptyStateText.text = "No container";
			emptyStateText.gameObject.SetActive(true);
			SetItemsVisible(false);
			SetManifestVisible(false);
			return;
		}

		bool hasItems = boxInfo.Items != null && boxInfo.Items.Count > 0;
		bool hasManifest = boxInfo.ManifestItems != null && boxInfo.ManifestItems.Count > 0;

		if (hasItems == false && hasManifest == false)
		{
			emptyStateText.text = "Empty";
			emptyStateText.gameObject.SetActive(true);
			SetItemsVisible(false);
			SetManifestVisible(false);
			return;
		}

		emptyStateText.gameObject.SetActive(false);
		SetItemsVisible(hasItems);
		SetManifestVisible(hasManifest);

		if (hasItems)
		{
			foreach (ItemContainerItemDisplayInfo item in boxInfo.Items)
			{
				ItemContainerItemRowView row = rowPool.Get().GetComponent<ItemContainerItemRowView>();
				row.transform.SetParent(itemRowsRoot, false);
				row.Setup(item);
			}
		}

		if (hasManifest)
		{
			foreach (ManifestContainerItemDisplayInfo manifestItem in boxInfo.ManifestItems)
			{
				ManifestContainerItemRowView row = manifestRowPool.Get().GetComponent<ManifestContainerItemRowView>();
				row.transform.SetParent(manifestRowsRoot, false);
				row.Setup(manifestItem);
			}
		}
	}

	private void EnsureBuilt()
	{
		if (built)
			return;

		TryBindFromExistingChildren();

		if (boxNameText != null && emptyStateText != null && headerRow != null && contentRoot != null)
		{
			EnsureSectionObjects();
			InitializePools();
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

		EnsureSectionObjects();
		InitializePools();
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
			itemsSectionTitleText ??= contentRoot.Find("ItemsSectionTitle")?.GetComponent<TextMeshProUGUI>();
			manifestSectionTitleText ??= contentRoot.Find("ManifestSectionTitle")?.GetComponent<TextMeshProUGUI>();

			itemRowsRoot ??= contentRoot.Find("ItemRows") as RectTransform;
			manifestRowsRoot ??= contentRoot.Find("ManifestRows") as RectTransform;

			if (headerRow == null)
				headerRow = contentRoot.Find("HeaderRow")?.GetComponent<ItemContainerItemRowView>();

			if (manifestHeaderRow == null)
				manifestHeaderRow = contentRoot.Find("ManifestHeaderRow")?.GetComponent<ManifestContainerItemRowView>();

			if (emptyStateText == null)
				emptyStateText = contentRoot.Find("EmptyState")?.GetComponent<TextMeshProUGUI>();
		}
	}

	private void EnsureSectionObjects()
	{
		itemsSectionTitleText ??= CreateSectionTitle("ItemsSectionTitle", "Items", contentRoot);
		headerRow ??= CreateHeaderRow();
		itemRowsRoot ??= CreateRowsRoot("ItemRows", contentRoot);
		manifestSectionTitleText ??= CreateSectionTitle("ManifestSectionTitle", "Manifest", contentRoot);
		manifestHeaderRow ??= CreateManifestHeaderRow();
		manifestRowsRoot ??= CreateRowsRoot("ManifestRows", contentRoot);

		itemsSectionTitleText.transform.SetSiblingIndex(0);
		headerRow.transform.SetSiblingIndex(1);
		itemRowsRoot.transform.SetSiblingIndex(2);
		manifestSectionTitleText.transform.SetSiblingIndex(3);
		manifestHeaderRow.transform.SetSiblingIndex(4);
		manifestRowsRoot.transform.SetSiblingIndex(5);

		headerRow.SetupHeader();
		manifestHeaderRow.SetupHeader();
	}

	private void InitializePools()
	{
		rowPool ??= new GameObjectPool(6, CreateRowObject);
		manifestRowPool ??= new GameObjectPool(6, CreateManifestRowObject);
		built = true;
	}

	private void SetItemsVisible(bool visible)
	{
		if (itemsSectionTitleText != null)
			itemsSectionTitleText.gameObject.SetActive(visible);
		if (headerRow != null)
			headerRow.gameObject.SetActive(visible);
		if (itemRowsRoot != null)
			itemRowsRoot.gameObject.SetActive(visible);
	}

	private void SetManifestVisible(bool visible)
	{
		if (manifestSectionTitleText != null)
			manifestSectionTitleText.gameObject.SetActive(visible);
		if (manifestHeaderRow != null)
			manifestHeaderRow.gameObject.SetActive(visible);
		if (manifestRowsRoot != null)
			manifestRowsRoot.gameObject.SetActive(visible);
	}

	private GameObject CreateRowObject()
	{
		ItemContainerItemRowView rowPrefab = Resources.Load<ItemContainerItemRowView>(RowPrefabPath);
		if (rowPrefab != null)
			return Instantiate(rowPrefab.gameObject, itemRowsRoot);

		GameObject rowObject = new("ItemContainerItemRow", typeof(RectTransform), typeof(ItemContainerItemRowView));
		rowObject.transform.SetParent(itemRowsRoot, false);
		return rowObject;
	}

	private GameObject CreateManifestRowObject()
	{
		ManifestContainerItemRowView rowPrefab = Resources.Load<ManifestContainerItemRowView>(ManifestRowPrefabPath);
		if (rowPrefab != null)
			return Instantiate(rowPrefab.gameObject, manifestRowsRoot);

		GameObject rowObject = new("ManifestContainerItemRow", typeof(RectTransform), typeof(ManifestContainerItemRowView));
		rowObject.transform.SetParent(manifestRowsRoot, false);
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

	private ManifestContainerItemRowView CreateManifestHeaderRow()
	{
		ManifestContainerItemRowView rowPrefab = Resources.Load<ManifestContainerItemRowView>(ManifestRowPrefabPath);
		if (rowPrefab != null)
			return Instantiate(rowPrefab, contentRoot);

		GameObject rowObject = new("ManifestHeaderRow", typeof(RectTransform), typeof(ManifestContainerItemRowView));
		rowObject.transform.SetParent(contentRoot, false);
		return rowObject.GetComponent<ManifestContainerItemRowView>();
	}

	private static RectTransform CreateRowsRoot(string name, Transform parent)
	{
		GameObject root = new(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
		root.transform.SetParent(parent, false);

		RectTransform rect = root.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.sizeDelta = Vector2.zero;

		VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
		layout.spacing = 4f;
		layout.padding = new RectOffset(0, 0, 0, 0);
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		return rect;
	}

	private static TextMeshProUGUI CreateSectionTitle(string name, string text, Transform parent)
	{
		TextMeshProUGUI title = CreateText(name, parent, 18f, FontStyles.Bold);
		title.text = text;
		return title;
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
