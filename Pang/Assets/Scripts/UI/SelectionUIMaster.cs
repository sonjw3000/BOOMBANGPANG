using System;
using System.Collections.Generic;
using System.Text;
using Assets.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectionUIMaster : MonoBehaviour
{
	[Header("Select UIs")]
	[SerializeField] private SelectCardUI cardUI = null;
	[SerializeField] private SelectDetailUI detailUI = null;
	[SerializeField] private DetailWindowManager detailWindowManager = null;

	[Header("Detail Contents")]
	[SerializeField] private DetailContentBase[] detailContents;

	[Header("World Highlight")]
	[SerializeField] private GameObject selectedTilePrefab;
	[SerializeField] private GameObject interactionTilePrefab;
	[SerializeField] private float selectedTileHeight = 0.03f;
	[SerializeField] private float interactionTileHeight = 0.035f;
	[SerializeField] private float interactionLabelHeight = 0.04f;
	[SerializeField] private Vector3 selectedTileScale = new(1.08f, 1f, 1.08f);
	[SerializeField] private Vector3 interactionTileScale = new(0.72f, 1f, 0.72f);
	[SerializeField] private float interactionLabelFontSize = 3.6f;
	[SerializeField] private float interactionLabelScale = 0.2f;
	[SerializeField] private Color interactionLabelColor = Color.white;
	[SerializeField] private int interactionHighlightPoolSize = 8;

	private readonly List<Type> providerTypes = new();

	private UIProviderBase currentProvider = null;
	private GameObject currentObj = null;
	private GameObject selectionHighlightRoot = null;
	private GameObject selectedHighlight = null;
	private GameObjectPool interactionHighlightPool = null;
	private GameObjectPool interactionLabelPool = null;
	private RectTransform modeHudRoot = null;
	private TextMeshProUGUI modeDomainText = null;
	private TextMeshProUGUI modeActionText = null;
	private Button buildingDetailsButton = null;
	private TextMeshProUGUI buildingDetailsButtonText = null;
	private ZoneOverlayController zoneOverlayController = null;
	private BuildingPlacementOverlayController buildingPlacementOverlayController = null;

	private InteractionContext Interaction => GameContext.HasInstance ? GameContext.Instance.InteractionCtx : null;

	private void Awake()
	{
		providerTypes.Add(typeof(CargoPortUIProvider));
		providerTypes.Add(typeof(AirlockUIProvider));
		providerTypes.Add(typeof(PackingStationUIProvider));
		providerTypes.Add(typeof(RocketUIProvider));
		providerTypes.Add(typeof(ShelfUIProvider));
		providerTypes.Add(typeof(BoxPoolUIProvider));
		providerTypes.Add(typeof(RobotWorkerUIProvider));
		providerTypes.Add(typeof(HumanWorkerUIProvider));
		providerTypes.Add(typeof(ZoneUIProvider));
		providerTypes.Add(typeof(BuildingUIProvider));

		EnsureRuntimeZoneDetailContent();
		EnsureRuntimeBuildingDetailContent();
		EnsureRuntimeAirlockDetailContent();
		EnsureDetailWindowManager();
		EnsureHighlightRoot();
		EnsureModeHud();
		EnsureModeDependencies();

		if (Interaction != null)
		{
			Interaction.OnItemSelected += OnSelected;
			Interaction.OnModeChanged += HandleInteractionModeChanged;
		}

		cardUI.FocusButton.onClick.AddListener(OnFocusBtnClicked);
		cardUI.DetailsButton.onClick.AddListener(OnDetailClicked);
		if (buildingDetailsButton != null)
			buildingDetailsButton.onClick.AddListener(HandleBuildingDetailsClicked);

		RefreshModeHud();
	}

	private void OnDisable()
	{
		cardUI.DetailsButton.onClick.RemoveListener(OnDetailClicked);
		cardUI.FocusButton.onClick.RemoveListener(OnFocusBtnClicked);
		if (buildingDetailsButton != null)
			buildingDetailsButton.onClick.RemoveListener(HandleBuildingDetailsClicked);

		if (Interaction != null)
		{
			Interaction.OnItemSelected -= OnSelected;
			Interaction.OnModeChanged -= HandleInteractionModeChanged;
		}

		if (zoneOverlayController != null)
		{
			zoneOverlayController.ActiveBuildingChanged -= HandleActiveBuildingChanged;
			zoneOverlayController.BuildingModeChanged -= HandleBuildingModeChanged;
		}

		HideWorldHighlights();
	}

	private void Update()
	{
		if (currentProvider != null)
			currentProvider.OnUpdate();
	}

	private void OnSelected(GameObject gridObj)
	{
		currentObj = gridObj;
		GetProvider();
		SelectionChange();
		RefreshWorldHighlights();
		RefreshModeHud();
	}

	private bool GetProvider()
	{
		currentProvider = null;
		if (currentObj == null)
			return false;

		currentProvider = CreateBestProvider(currentObj);
		if (currentProvider != null)
			return true;

		Debug.LogWarning($"No suitable UI Provider found for the selected object, Target: {currentObj.name}");
		return false;
	}

	public void SelectionChange()
	{
		if (currentProvider == null || currentObj == null)
		{
			DisableCard();
			return;
		}

		currentProvider.LinkObject(currentObj);
		currentProvider.BuildInfoBlocks();

		cardUI.SetUpCard(currentProvider);
		cardUI.gameObject.SetActive(true);
	}

	private void DisableCard()
	{
		cardUI.ClearCard();
		cardUI.gameObject.SetActive(false);
	}

	public void OnDetailClicked()
	{
		OpenDetailWindow(currentObj);
	}

	public void SelectAndShowDetail(GameObject targetObj)
	{
		if (targetObj == null)
		{
			return;
		}

		if (Interaction != null)
		{
			Interaction.SelectObject(targetObj);
		}
		else
		{
			currentObj = targetObj;
			GetProvider();
			SelectionChange();
			RefreshWorldHighlights();
			RefreshModeHud();
		}

		OpenDetailWindow(targetObj);
	}

	public void OnFocusBtnClicked()
	{
	}

	public void ShowDetailForObject(GameObject targetObj)
	{
		if (targetObj == null)
			return;

		OpenDetailWindow(targetObj);
	}

	public SelectDetailUI OpenDetailWindow(GameObject targetObj)
	{
		if (targetObj == null)
			return null;

		UIProviderBase provider = CreateBestProvider(targetObj);
		if (provider == null)
			return null;

		provider.LinkObject(targetObj);
		provider.BuildInfoBlocks();
		return detailWindowManager != null ? detailWindowManager.OpenDetail(targetObj, provider) : null;
	}

	private UIProviderBase CreateBestProvider(GameObject targetObj)
	{
		Type bestProviderType = null;
		int bestDistance = int.MaxValue;

		for (int i = 0; i < providerTypes.Count; ++i)
		{
			UIProviderBase provider = CreateProvider(providerTypes[i]);
			if (provider == null)
				continue;

			int distance = GetMatchDistance(targetObj, provider.TargetType);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				bestProviderType = providerTypes[i];
			}
		}

		return CreateProvider(bestProviderType);
	}

	private static UIProviderBase CreateProvider(Type providerType)
	{
		if (providerType == null)
			return null;

		if (Activator.CreateInstance(providerType) is UIProviderBase provider)
			return provider;

		return null;
	}

	private int GetMatchDistance(GameObject targetObj, System.Type candidateType)
	{
		if (targetObj == null || candidateType == null)
			return int.MaxValue;

		Component matchedComponent = targetObj.GetComponent(candidateType);
		if (matchedComponent == null)
			return int.MaxValue;

		System.Type currentType = matchedComponent.GetType();
		int distance = 0;
		while (currentType != null)
		{
			if (currentType == candidateType)
				return distance;

			currentType = currentType.BaseType;
			distance++;
		}

		return int.MaxValue;
	}

	private void EnsureHighlightRoot()
	{
		if (selectionHighlightRoot != null)
			return;

		selectionHighlightRoot = new GameObject("SelectionHighlightRoot");
		selectionHighlightRoot.transform.SetParent(transform, false);
		if (interactionTilePrefab != null)
			interactionHighlightPool = new GameObjectPool(interactionHighlightPoolSize, () => CreateHighlight("InteractionHighlight", interactionTilePrefab));
		interactionLabelPool = new GameObjectPool(interactionHighlightPoolSize, CreateInteractionLabel);
	}

	private void EnsureDetailWindowManager()
	{
		if (detailWindowManager == null)
			detailWindowManager = GetComponent<DetailWindowManager>();

		if (detailWindowManager == null)
			detailWindowManager = gameObject.AddComponent<DetailWindowManager>();

		detailWindowManager.Initialize(detailUI);
	}

	private void EnsureRuntimeBuildingDetailContent()
	{
		if (detailUI == null)
			return;

		foreach (DetailContentBase detailContent in detailContents)
		{
			if (detailContent is BuildingDetailContent)
				return;
		}

		UIWindow detailWindow = detailUI.GetComponentInChildren<UIWindow>(true);
		Transform parent = detailWindow != null && detailWindow.ContentRoot != null
			? detailWindow.ContentRoot
			: detailUI.transform;

		GameObject detailRoot = new("RuntimeBuildingDetailContent", typeof(RectTransform), typeof(BuildingDetailContent));
		detailRoot.transform.SetParent(parent, false);
		detailRoot.SetActive(false);

		BuildingDetailContent buildingDetail = detailRoot.GetComponent<BuildingDetailContent>();
		var contents = new List<DetailContentBase>(detailContents ?? System.Array.Empty<DetailContentBase>())
		{
			buildingDetail
		};
		detailContents = contents.ToArray();
	}

	private void EnsureRuntimeZoneDetailContent()
	{
		if (detailUI == null)
			return;

		foreach (DetailContentBase detailContent in detailContents)
		{
			if (detailContent is ZoneDetailContent)
				return;
		}

		UIWindow detailWindow = detailUI.GetComponentInChildren<UIWindow>(true);
		Transform parent = detailWindow != null && detailWindow.ContentRoot != null
			? detailWindow.ContentRoot
			: detailUI.transform;

		GameObject detailRoot = new("RuntimeZoneDetailContent", typeof(RectTransform), typeof(ZoneDetailContent));
		detailRoot.transform.SetParent(parent, false);
		detailRoot.SetActive(false);

		ZoneDetailContent zoneDetail = detailRoot.GetComponent<ZoneDetailContent>();
		var contents = new List<DetailContentBase>(detailContents ?? System.Array.Empty<DetailContentBase>())
		{
			zoneDetail
		};
		detailContents = contents.ToArray();
	}

	private void EnsureModeDependencies()
	{
		if (zoneOverlayController == null)
		{
			zoneOverlayController = FindFirstObjectByType<ZoneOverlayController>(FindObjectsInactive.Include);
			if (zoneOverlayController != null)
			{
				zoneOverlayController.ActiveBuildingChanged -= HandleActiveBuildingChanged;
				zoneOverlayController.ActiveBuildingChanged += HandleActiveBuildingChanged;
				zoneOverlayController.BuildingModeChanged -= HandleBuildingModeChanged;
				zoneOverlayController.BuildingModeChanged += HandleBuildingModeChanged;
			}
		}

		if (buildingPlacementOverlayController == null)
			buildingPlacementOverlayController = FindFirstObjectByType<BuildingPlacementOverlayController>(FindObjectsInactive.Include);
	}

	private void EnsureModeHud()
	{
		if (modeHudRoot != null)
			return;

		Canvas canvas = GetComponentInParent<Canvas>();
		if (canvas == null)
			canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

		if (canvas == null)
			return;

		GameObject rootObject = new("InteractionModeHud", typeof(RectTransform));
		modeHudRoot = rootObject.GetComponent<RectTransform>();
		modeHudRoot.SetParent(canvas.transform, false);
		modeHudRoot.anchorMin = new Vector2(0.5f, 1f);
		modeHudRoot.anchorMax = new Vector2(0.5f, 1f);
		modeHudRoot.pivot = new Vector2(0.5f, 1f);
		modeHudRoot.anchoredPosition = new Vector2(0f, -20f);
		modeHudRoot.sizeDelta = new Vector2(320f, 96f);

		GameObject domainObject = new("ModeDomain", typeof(RectTransform), typeof(TextMeshProUGUI));
		domainObject.transform.SetParent(modeHudRoot, false);
		modeDomainText = domainObject.GetComponent<TextMeshProUGUI>();
		modeDomainText.alignment = TextAlignmentOptions.Center;
		modeDomainText.fontSize = 28f;
		modeDomainText.color = Color.white;
		modeDomainText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
		modeDomainText.rectTransform.anchorMax = new Vector2(1f, 1f);
		modeDomainText.rectTransform.offsetMin = Vector2.zero;
		modeDomainText.rectTransform.offsetMax = Vector2.zero;

		GameObject actionObject = new("ModeAction", typeof(RectTransform), typeof(TextMeshProUGUI));
		actionObject.transform.SetParent(modeHudRoot, false);
		modeActionText = actionObject.GetComponent<TextMeshProUGUI>();
		modeActionText.alignment = TextAlignmentOptions.Center;
		modeActionText.fontSize = 18f;
		modeActionText.color = new Color(0.82f, 0.88f, 0.95f, 1f);
		modeActionText.rectTransform.anchorMin = new Vector2(0f, 0f);
		modeActionText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
		modeActionText.rectTransform.offsetMin = Vector2.zero;
		modeActionText.rectTransform.offsetMax = Vector2.zero;

		GameObject buttonObject = new("BuildingModeDetailsButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
		buttonObject.transform.SetParent(canvas.transform, false);
		RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
		buttonRect.anchorMin = new Vector2(0.5f, 0f);
		buttonRect.anchorMax = new Vector2(0.5f, 0f);
		buttonRect.pivot = new Vector2(0.5f, 0f);
		buttonRect.anchoredPosition = new Vector2(0f, 40f);
		buttonRect.sizeDelta = new Vector2(180f, 42f);

		Image buttonImage = buttonObject.GetComponent<Image>();
		buttonImage.color = new Color(0.18f, 0.42f, 0.7f, 0.92f);

		buildingDetailsButton = buttonObject.GetComponent<Button>();
		ColorBlock colors = buildingDetailsButton.colors;
		colors.disabledColor = new Color(0.26f, 0.26f, 0.26f, 0.85f);
		buildingDetailsButton.colors = colors;

		GameObject buttonTextObject = new("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
		buttonTextObject.transform.SetParent(buttonObject.transform, false);
		buildingDetailsButtonText = buttonTextObject.GetComponent<TextMeshProUGUI>();
		buildingDetailsButtonText.text = "Details";
		buildingDetailsButtonText.alignment = TextAlignmentOptions.Center;
		buildingDetailsButtonText.fontSize = 22f;
		buildingDetailsButtonText.color = Color.white;
		buildingDetailsButtonText.rectTransform.anchorMin = Vector2.zero;
		buildingDetailsButtonText.rectTransform.anchorMax = Vector2.one;
		buildingDetailsButtonText.rectTransform.offsetMin = Vector2.zero;
		buildingDetailsButtonText.rectTransform.offsetMax = Vector2.zero;
	}

	private void HandleInteractionModeChanged(InteractionContext.InteractionDomain domain, InteractionContext.InteractionAction action)
	{
		RefreshModeHud();
	}

	private void HandleActiveBuildingChanged(Building building)
	{
		RefreshModeHud();
	}

	private void HandleBuildingModeChanged(bool active)
	{
		RefreshModeHud();
	}

	private void HandleBuildingDetailsClicked()
	{
		EnsureModeDependencies();
		if (zoneOverlayController == null || buildingPlacementOverlayController == null)
			return;

		Building activeBuilding = zoneOverlayController.CurrentBuilding;
		if (activeBuilding == null)
			return;

		BuildingSelectionProxy proxy = buildingPlacementOverlayController.GetSelectionProxy(activeBuilding);
		if (proxy == null)
			return;

		ShowDetailForObject(proxy.gameObject);
	}

	private void RefreshModeHud()
	{
		if (Interaction == null)
			return;

		EnsureModeDependencies();

		if (modeDomainText != null)
		{
			modeDomainText.text = Interaction.Domain == InteractionContext.InteractionDomain.Building
				? "Building Mode"
				: "Facility Mode";
		}

		if (modeActionText != null)
		{
			modeActionText.text = Interaction.Action switch
			{
				InteractionContext.InteractionAction.Install => "Install",
				InteractionContext.InteractionAction.ZoneEdit => "Zone Edit",
				_ => "Select",
			};
		}

		if (buildingDetailsButton != null)
		{
			bool isBuildingMode = Interaction.Domain == InteractionContext.InteractionDomain.Building;
			bool hasActiveBuilding = zoneOverlayController != null && zoneOverlayController.CurrentBuilding != null;
			buildingDetailsButton.interactable = isBuildingMode && hasActiveBuilding;
			buildingDetailsButton.gameObject.SetActive(true);
		}
	}

	private void RefreshWorldHighlights()
	{
		EnsureHighlightRoot();
		HideWorldHighlights();

		if (currentObj == null)
			return;

		if (currentObj.TryGetComponent<IGridPlaceable>(out var placeable))
		{
			selectedHighlight ??= CreateHighlight("SelectedHighlight", selectedTilePrefab);
			if (selectedHighlight != null)
			{
				selectedHighlight.transform.position = BuildHighlightPosition(placeable.GridPosition, selectedTileHeight);
				selectedHighlight.transform.localScale = selectedTileScale;
				selectedHighlight.SetActive(true);
			}
		}

		if (currentObj.TryGetComponent<IInteractionPoint>(out var interactable) == false)
			return;

		var points = interactable.InteractionPoints;
		for (int i = 0; i < points.Count; ++i)
		{
			if (interactionHighlightPool != null)
			{
				GameObject highlight = interactionHighlightPool.Get();
				highlight.transform.position = BuildHighlightPosition(points[i].Point, interactionTileHeight);
				highlight.transform.localScale = interactionTileScale;
			}

			GameObject label = interactionLabelPool.Get();
			ConfigureInteractionLabel(label, points[i]);
		}
	}

	private void HideWorldHighlights()
	{
		if (selectedHighlight != null)
			selectedHighlight.SetActive(false);

		interactionHighlightPool?.ReleaseAll();
		interactionLabelPool?.ReleaseAll();
	}

	private GameObject CreateHighlight(string objectName, GameObject prefab)
	{
		if (prefab == null)
			return null;

		GameObject highlight = Instantiate(prefab, selectionHighlightRoot.transform);
		highlight.name = objectName;

		if (highlight.TryGetComponent<Collider>(out var collider))
			Destroy(collider);

		highlight.SetActive(false);
		return highlight;
	}

	private GameObject CreateInteractionLabel()
	{
		GameObject label = new("InteractionLabel");
		label.transform.SetParent(selectionHighlightRoot.transform, false);

		var text = label.AddComponent<TextMeshPro>();
		text.alignment = TextAlignmentOptions.Center;
		text.fontSize = interactionLabelFontSize;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.color = interactionLabelColor;

		label.SetActive(false);
		return label;
	}

	private void ConfigureInteractionLabel(GameObject label, InteractionPoint point)
	{
		if (label == null)
			return;

		var text = label.GetComponent<TextMeshPro>();
		text.text = BuildInteractionLabel(point.InteractionKind);
		text.color = interactionLabelColor;

		label.transform.position = BuildHighlightPosition(point.Point, interactionLabelHeight);
		label.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
		label.transform.localScale = Vector3.one * interactionLabelScale;
	}

	private static string BuildInteractionLabel(InteractionKind interactionKind)
	{
		if (interactionKind == InteractionKind.None)
			return string.Empty;

		StringBuilder builder = new();
		AppendInteractionLabel(builder, interactionKind, InteractionKind.Pick, "PICK");
		AppendInteractionLabel(builder, interactionKind, InteractionKind.Put, "PUT");
		AppendInteractionLabel(builder, interactionKind, InteractionKind.Work, "WORK");
		AppendInteractionLabel(builder, interactionKind, InteractionKind.Charge, "CHARGE");
		AppendInteractionLabel(builder, interactionKind, InteractionKind.Enter, "ENTER");
		return builder.ToString();
	}

	private void EnsureRuntimeAirlockDetailContent()
	{
		if (detailUI == null)
			return;

		foreach (DetailContentBase detailContent in detailContents)
		{
			if (detailContent is AirlockDetailContent)
				return;
		}

		UIWindow detailWindow = detailUI.GetComponentInChildren<UIWindow>(true);
		Transform parent = detailWindow != null && detailWindow.ContentRoot != null
			? detailWindow.ContentRoot
			: detailUI.transform;

		GameObject detailRoot = new("RuntimeAirlockDetailContent", typeof(RectTransform), typeof(AirlockDetailContent));
		detailRoot.transform.SetParent(parent, false);
		detailRoot.SetActive(false);

		AirlockDetailContent airlockDetail = detailRoot.GetComponent<AirlockDetailContent>();
		var contents = new List<DetailContentBase>(detailContents ?? System.Array.Empty<DetailContentBase>())
		{
			airlockDetail
		};
		detailContents = contents.ToArray();
	}

	private static void AppendInteractionLabel(StringBuilder builder, InteractionKind source, InteractionKind target, string label)
	{
		if (source.HasFlag(target) == false)
			return;

		if (builder.Length > 0)
			builder.Append(" / ");

		builder.Append(label);
	}

	private static Vector3 BuildHighlightPosition(in Unity.Mathematics.int3 gridPos, float y)
	{
		return new Vector3(gridPos.x, gridPos.y + y, gridPos.z);
	}
}
