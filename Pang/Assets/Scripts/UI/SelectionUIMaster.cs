using System;
using System.Collections.Generic;
using System.Text;
using AYellowpaper.SerializedCollections;
using Assets.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
using GlobalStatusHud = UniverseLogistics.UI.Toolkit.GlobalStatusHud;
using SelectionCardHud = UniverseLogistics.UI.Toolkit.SelectionCardHud;

public class SelectionUIMaster : MonoBehaviour
{
	private enum WorldHighlightType
	{
		SelectedTile,
		InteractionTile,
		InteractionLabel,
	}

	[System.Serializable]
	private struct WorldHighlightVisualConfig
	{
		public GameObject Prefab;
		public float Height;
		public Vector3 Scale;
		public float FontSize;
		public Color Color;

		public WorldHighlightVisualConfig(GameObject prefab, float height, Vector3 scale, float fontSize, Color color)
		{
			Prefab = prefab;
			Height = height;
			Scale = scale;
			FontSize = fontSize;
			Color = color;
		}
	}

	[Header("World Highlight")]
	[SerializedDictionary("Highlight", "Visual")]
	[SerializeField] private SerializedDictionary<WorldHighlightType, WorldHighlightVisualConfig> highlightVisuals = new();
	[SerializeField] private VisualTreeAsset interactionModeHudVisualTreeAsset;
	[SerializeField] private PanelSettings interactionModeHudPanelSettings;
	[SerializeField] private int interactionModeHudSortingOrder = 110;
	[SerializeField] private int interactionHighlightPoolSize = 8;

	private readonly List<Type> providerTypes = new();

	private UIProviderBase currentProvider = null;
	private GameObject currentObj = null;
	private GameObject selectionHighlightRoot = null;
	private GameObject selectedHighlight = null;
	private GameObjectPool interactionHighlightPool = null;
	private GameObjectPool interactionLabelPool = null;
	private UIDocument interactionModeHudDocument = null;
	private Label modeDomainText = null;
	private Label modeActionText = null;
	private SelectionCardHud selectionCardHud = null;

	private InteractionContext Interaction => GameContext.HasInstance ? GameContext.Instance.InteractionCtx : null;

	private void Awake()
	{
		EnsureHighlightVisuals();
		providerTypes.Add(typeof(CapsuleBufferUIProvider));
		providerTypes.Add(typeof(PowerHubUIProvider));
		providerTypes.Add(typeof(CargoPortUIProvider));
		providerTypes.Add(typeof(AirlockUIProvider));
		providerTypes.Add(typeof(PackingStationUIProvider));
		providerTypes.Add(typeof(RocketUIProvider));
		providerTypes.Add(typeof(ShelfUIProvider));
		providerTypes.Add(typeof(BoxPoolUIProvider));
		providerTypes.Add(typeof(RobotWorkerUIProvider));
		providerTypes.Add(typeof(HumanWorkerUIProvider));
		providerTypes.Add(typeof(AreaUIProvider));
		providerTypes.Add(typeof(BuildingUIProvider));

		EnsureHighlightRoot();
		EnsureModeHud();

		if (Interaction != null)
		{
			Interaction.OnItemSelected += OnSelected;
			Interaction.OnModeChanged += HandleInteractionModeChanged;
		}

		EnsureSelectionCardHud();

		RefreshModeHud();
	}

	private void OnValidate()
	{
		EnsureHighlightVisuals();
	}

	private void OnDisable()
	{
		selectionCardHud?.Hide();
		selectionCardHud?.SetActions(null, null);

		if (Interaction != null)
		{
			Interaction.OnItemSelected -= OnSelected;
			Interaction.OnModeChanged -= HandleInteractionModeChanged;
		}

		HideWorldHighlights();
	}

	private void Update()
	{
		if (currentProvider != null)
		{
			currentProvider.OnUpdate();
			if (EnsureSelectionCardHud())
			{
				if (selectionCardHud.Refresh(currentProvider) == false)
					selectionCardHud.Show(currentProvider);
			}
		}
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

		if (EnsureSelectionCardHud())
		{
			selectionCardHud.Show(currentProvider);
			return;
		}

		Debug.LogError("[SelectionUIMaster] SelectionCardHud is unavailable.", this);
	}

	private void DisableCard()
	{
		selectionCardHud?.Hide();
	}

	private bool EnsureSelectionCardHud()
	{
		if (selectionCardHud == null)
		{
			GlobalStatusHud globalHud = FindAnyObjectByType<GlobalStatusHud>(FindObjectsInactive.Include);
			selectionCardHud = globalHud != null ? globalHud.SelectionCard : null;
		}

		if (selectionCardHud == null || selectionCardHud.IsBound == false)
			return false;

		selectionCardHud.SetActions(OnFocusBtnClicked, OnDetailClicked);
		return true;
	}

	public void OnDetailClicked()
	{
		if (selectionCardHud == null || selectionCardHud.ToggleInspector(currentProvider) == false)
			Debug.LogWarning("[SelectionUIMaster] Toolkit inspector is unavailable for the selected object.", this);
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

		if (selectionCardHud == null || selectionCardHud.ExpandInspector(currentProvider) == false)
			Debug.LogWarning("[SelectionUIMaster] Toolkit inspector is unavailable for the selected object.", this);
	}

	public void OnFocusBtnClicked()
	{
		if (currentObj == null) return;
		OrbitCamera orbitCamera = Camera.main != null ? Camera.main.GetComponent<OrbitCamera>() : null;
		orbitCamera ??= FindAnyObjectByType<OrbitCamera>();
		if (orbitCamera != null)
			orbitCamera._GoalTargetPos = currentObj.transform.position;
	}

	public void ShowDetailForObject(GameObject targetObj)
	{
		if (targetObj == null)
			return;

		SelectAndShowDetail(targetObj);
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
		{
			EnsureHighlightPools();
			return;
		}

		selectionHighlightRoot = new GameObject("SelectionHighlightRoot");
		selectionHighlightRoot.transform.SetParent(transform, false);
		EnsureHighlightPools();
	}

	private void EnsureModeHud()
	{
		if (interactionModeHudDocument != null)
			return;

		if (interactionModeHudVisualTreeAsset == null || interactionModeHudPanelSettings == null)
		{
			Debug.LogError("[SelectionUIMaster] InteractionModeHud Toolkit assets are missing.", this);
			return;
		}

		GameObject documentObject = new("InteractionModeHudDocument");
		documentObject.SetActive(false);
		documentObject.transform.SetParent(transform, false);
		interactionModeHudDocument = documentObject.AddComponent<UIDocument>();
		interactionModeHudDocument.panelSettings = interactionModeHudPanelSettings;
		interactionModeHudDocument.visualTreeAsset = interactionModeHudVisualTreeAsset;
		interactionModeHudDocument.sortingOrder = interactionModeHudSortingOrder;
		documentObject.SetActive(true);

		VisualElement root = interactionModeHudDocument.rootVisualElement;
		modeDomainText = root.Q<Label>("interaction-mode-domain");
		modeActionText = root.Q<Label>("interaction-mode-action");
		if (modeDomainText == null || modeActionText == null)
			Debug.LogError("[SelectionUIMaster] InteractionModeHud UXML elements are missing.", this);
	}

	private void HandleInteractionModeChanged(InteractionContext.InteractionDomain domain, InteractionContext.InteractionAction action)
	{
		RefreshModeHud();
	}

	private void RefreshModeHud()
	{
		if (Interaction == null)
			return;

		EnsureModeHud();

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
				InteractionContext.InteractionAction.AreaEdit => "Area Edit",
				InteractionContext.InteractionAction.LinkEdit => "Link Edit",
				_ => "Select",
			};
		}

	}

	private void RefreshWorldHighlights()
	{
		EnsureHighlightRoot();
		HideWorldHighlights();

		if (currentObj == null)
			return;

		WorldHighlightVisualConfig selectedVisual = GetHighlightVisual(WorldHighlightType.SelectedTile);
		if (currentObj.TryGetComponent<IGridPlaceable>(out var placeable))
		{
			selectedHighlight ??= CreateHighlight("SelectedHighlight", selectedVisual.Prefab);
			if (selectedHighlight != null)
			{
				selectedHighlight.transform.position = BuildHighlightPosition(placeable.GridPosition, selectedVisual.Height);
				selectedHighlight.transform.localScale = selectedVisual.Scale;
				selectedHighlight.SetActive(true);
			}
		}

		if (currentObj.TryGetComponent<IInteractionPoint>(out var interactable) == false)
			return;

		WorldHighlightVisualConfig interactionTileVisual = GetHighlightVisual(WorldHighlightType.InteractionTile);
		var points = interactable.InteractionPoints;
		for (int i = 0; i < points.Count; ++i)
		{
			if (interactionHighlightPool != null)
			{
				GameObject highlight = interactionHighlightPool.Get();
				if (highlight != null)
				{
					highlight.transform.position = BuildHighlightPosition(points[i].Point, interactionTileVisual.Height);
					highlight.transform.localScale = interactionTileVisual.Scale;
				}
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
		WorldHighlightVisualConfig visual = GetHighlightVisual(WorldHighlightType.InteractionLabel);
		GameObject label = CreateInteractionLabelObject(visual.Prefab);
		label.name = "InteractionLabel";
		if (label == null)
			return null;

		var text = label.GetComponent<TextMeshPro>();
		if (text == null)
		{
			Debug.LogError("[SelectionUIMaster] InteractionLabel prefab is missing a TextMeshPro component.", this);
			Destroy(label);
			return null;
		}

		text.alignment = TextAlignmentOptions.Center;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.overflowMode = TextOverflowModes.Overflow;
		text.fontSize = visual.FontSize;
		text.color = visual.Color;
		label.SetActive(false);
		return label;
	}

	private void ConfigureInteractionLabel(GameObject label, InteractionPoint point)
	{
		if (label == null)
			return;

		WorldHighlightVisualConfig visual = GetHighlightVisual(WorldHighlightType.InteractionLabel);
		var text = label.GetComponent<TextMeshPro>();
		if (text == null)
			return;

		text.text = BuildInteractionLabel(point.InteractionKind);
		text.color = visual.Color;

		label.transform.position = BuildHighlightPosition(point.Point, visual.Height);
		label.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
		label.transform.localScale = visual.Scale;
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

	private void EnsureHighlightVisuals()
	{
		highlightVisuals ??= new SerializedDictionary<WorldHighlightType, WorldHighlightVisualConfig>();

		SetMissingHighlightVisual(
			WorldHighlightType.SelectedTile,
			new WorldHighlightVisualConfig(
				null,
				0.03f,
				new Vector3(1.08f, 1f, 1.08f),
				0f,
				Color.white));

		SetMissingHighlightVisual(
			WorldHighlightType.InteractionTile,
			new WorldHighlightVisualConfig(
				null,
				0.035f,
				new Vector3(0.72f, 1f, 0.72f),
				0f,
				Color.white));

		SetMissingHighlightVisual(
			WorldHighlightType.InteractionLabel,
			new WorldHighlightVisualConfig(
				null,
				0.04f,
				Vector3.one * 0.2f,
				3.6f,
				Color.white));
	}

	private void EnsureHighlightPools()
	{
		WorldHighlightVisualConfig interactionTileVisual = GetHighlightVisual(WorldHighlightType.InteractionTile);
		if (interactionHighlightPool == null && interactionTileVisual.Prefab != null)
			interactionHighlightPool = new GameObjectPool(interactionHighlightPoolSize, () => CreateHighlight("InteractionHighlight", interactionTileVisual.Prefab));

		if (interactionLabelPool == null)
			interactionLabelPool = new GameObjectPool(interactionHighlightPoolSize, CreateInteractionLabel);
	}

	private GameObject CreateInteractionLabelObject(GameObject configuredPrefab)
	{
		if (configuredPrefab != null)
		{
			GameObject configuredLabel = Instantiate(configuredPrefab, selectionHighlightRoot.transform);
			if (configuredLabel != null && configuredLabel.GetComponent<TextMeshPro>() != null)
				return configuredLabel;

			Debug.LogWarning("[SelectionUIMaster] InteractionLabel prefab is invalid. Falling back to a runtime TextMeshPro label.", this);
			if (configuredLabel != null)
				Destroy(configuredLabel);
		}
		else
		{
			Debug.LogWarning("[SelectionUIMaster] InteractionLabel prefab is missing. Falling back to a runtime TextMeshPro label.", this);
		}

		GameObject fallbackLabel = new("InteractionLabel");
		fallbackLabel.transform.SetParent(selectionHighlightRoot.transform, false);
		fallbackLabel.AddComponent<TextMeshPro>();
		return fallbackLabel;
	}

	private WorldHighlightVisualConfig GetHighlightVisual(WorldHighlightType highlightType)
	{
		EnsureHighlightVisuals();
		return highlightVisuals.TryGetValue(highlightType, out WorldHighlightVisualConfig visual)
			? visual
			: default;
	}

	private void SetMissingHighlightVisual(WorldHighlightType highlightType, WorldHighlightVisualConfig visual)
	{
		if (highlightVisuals.ContainsKey(highlightType))
			return;

		highlightVisuals[highlightType] = visual;
	}

	private static T FindNamedComponent<T>(Transform root, string childName)
		where T : Component
	{
		if (root == null)
			return null;

		Transform child = root.Find(childName);
		return child != null ? child.GetComponent<T>() : null;
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
