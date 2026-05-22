using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class SelectionUIMaster : MonoBehaviour
{
	[Header("Select UIs")]
	[SerializeField] private SelectCardUI cardUI = null;
	[SerializeField] private SelectDetailUI detailUI = null;

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

	private readonly Dictionary<System.Type, UIProviderBase> providers = new();

	private UIProviderBase currentProvider = null;
	private DetailContentBase currentDetailContent = null;
	private GameObject currentObj = null;
	private GameObject selectionHighlightRoot = null;
	private GameObject selectedHighlight = null;
	private GameObjectPool interactionHighlightPool = null;
	private GameObjectPool interactionLabelPool = null;

	private void Awake()
	{
		providers[typeof(CargoPort)] = new CargoPortUIProvider();
		providers[typeof(PackingStation)] = new PackingStationUIProvider();
		providers[typeof(Rocket)] = new RocketUIProvider();
		providers[typeof(ShelfBase)] = new ShelfUIProvider();
		providers[typeof(BoxPool)] = new BoxPoolUIProvider();
		providers[typeof(RobotWorker)] = new RobotWorkerUIProvider();
		providers[typeof(HumanWorker)] = new HumanWorkerUIProvider();
		providers[typeof(ZoneSelectionProxy)] = new ZoneUIProvider();

		GameContext.Instance.InteractionCtx.OnItemSelected += OnSelected;

		cardUI.FocusButton.onClick.AddListener(OnFocusBtnClicked);
		cardUI.DetailsButton.onClick.AddListener(OnDetailClicked);

		EnsureHighlightRoot();
	}

	private void OnDisable()
	{
		cardUI.DetailsButton.onClick.RemoveListener(OnDetailClicked);
		cardUI.FocusButton.onClick.RemoveListener(OnFocusBtnClicked);

		if (GameContext.HasInstance && GameContext.Instance.InteractionCtx != null)
			GameContext.Instance.InteractionCtx.OnItemSelected -= OnSelected;

		HideWorldHighlights();
	}

	private void Update()
	{
		if (currentProvider != null)
		{
			currentProvider.OnUpdate();
		}
	}

	private void OnSelected(GameObject gridObj)
	{
		currentObj = gridObj;

		GetProvider();
		SelectionChange();
		RefreshWorldHighlights();
	}

	private bool GetProvider()
	{
		currentProvider = null;

		if (currentObj == null)
			return false;

		currentProvider = GetBestProvider();
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
			detailUI.gameObject.SetActive(false);
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
		if (currentObj == null || currentProvider == null)
		{
			detailUI.gameObject.SetActive(false);
			return;
		}

		if (currentProvider is ZoneUIProvider zoneProvider)
		{
			detailUI.SetZoneDetail(zoneProvider);
			detailUI.gameObject.SetActive(true);
			return;
		}

		currentDetailContent?.gameObject.SetActive(false);
		currentDetailContent = GetBestDetailContent();
		currentDetailContent?.SetProvider(currentProvider);

		if (currentDetailContent != null)
		{
			detailUI.SetDetailContent(currentDetailContent);
			detailUI.gameObject.SetActive(true);
		}
		else
		{
			string targetName = currentObj != null ? currentObj.name : "None";
			Debug.LogWarning($"No suitable UI DetailBuilder found for the selected object, Target: {targetName}");
		}
	}

	public void OnFocusBtnClicked()
	{
	}

	private UIProviderBase GetBestProvider()
	{
		UIProviderBase bestProvider = null;
		int bestDistance = int.MaxValue;

		foreach (UIProviderBase provider in providers.Values)
		{
			int distance = GetMatchDistance(provider.TargetType);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				bestProvider = provider;
			}
		}

		return bestProvider;
	}

	private DetailContentBase GetBestDetailContent()
	{
		DetailContentBase bestContent = null;
		int bestDistance = int.MaxValue;

		foreach (DetailContentBase content in detailContents)
		{
			int distance = GetMatchDistance(content.TargetType);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				bestContent = content;
			}
		}

		return bestContent;
	}

	private int GetMatchDistance(System.Type candidateType)
	{
		if (currentObj == null || candidateType == null)
			return int.MaxValue;

		Component matchedComponent = currentObj.GetComponent(candidateType);
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
		return builder.ToString();
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
