using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

public class AreaOverlayController : MonoBehaviour
{
	private sealed class AreaVisual
	{
		public GameObject Quad;
		public GameObject Label;
		public AreaSelectionProxy Proxy;
	}

	[FormerlySerializedAs("zoneManager")]
	[SerializeField] private AreaManager areaManager;
	[SerializeField] private int previewPoolSize = 8;
	[FormerlySerializedAs("zoneOverlayHeight")]
	[SerializeField] private float areaOverlayHeight = 0.02f;
	[SerializeField] private float labelHeight = 0.03f;
	[SerializeField] private float previewHeight = 0.035f;
	[SerializeField] private float overlayAlpha = 0.25f;
	[SerializeField] private Color invalidPreviewColor = new(1f, 0.25f, 0.25f, 0.4f);
	[SerializeField] private int currentFloor;
	[FormerlySerializedAs("selectionProxyPrefab")]
	[SerializeField] private AreaSelectionProxy selectionProxyPrefab;
	[SerializeField] private GameObject overlayQuadPrefab;
	[SerializeField] private GameObject overlayLabelPrefab;

	private readonly Dictionary<Area, AreaSelectionProxy> proxies = new();
	private readonly Dictionary<Area, AreaVisual> activeVisuals = new();
	private GameObjectPool quadPool;
	private GameObjectPool labelPool;
	private GameObject overlayRoot;
	private GameObject previewRoot;
	private GameObject proxyRoot;
	private GameObject previewQuad;
	private GameObject previewLabel;
	private bool isVisible;
	private AreaType activeAreaType = AreaType.RocketLanding;

	public AreaType ActiveAreaType => activeAreaType;
	public int CurrentFloor => currentFloor;

	private InteractionContext Interaction => GameContext.Instance.InteractionCtx;

	private void Awake()
	{
		areaManager ??= GetComponent<AreaManager>();
		overlayRoot = CreateRoot("AreaOverlayRoot");
		previewRoot = CreateRoot("AreaPreviewRoot");
		proxyRoot = CreateRoot("AreaProxyRoot");

		quadPool = new GameObjectPool(previewPoolSize, () => CreateQuad("AreaOverlayQuad", overlayRoot.transform));
		labelPool = new GameObjectPool(previewPoolSize, () => CreateLabel("AreaOverlayLabel", overlayRoot.transform));
		previewQuad = CreateQuad("AreaPreviewQuad", previewRoot.transform);
		previewLabel = CreateLabel("AreaPreviewLabel", previewRoot.transform);
		HidePreview();
		overlayRoot.SetActive(false);
		previewRoot.SetActive(false);

		if (areaManager != null)
		{
			areaManager.OnAreaAdded += HandleAreaListChanged;
			areaManager.OnAreaChanged += HandleAreaListChanged;
			areaManager.OnAreaRemoved += HandleAreaRemoved;
			areaManager.OnAreasRebuilt += RefreshVisibleAreas;
		}

		Interaction.OnResolveSelectionFallback += ResolveAreaSelection;
		Interaction.OnModeChanged += HandleInteractionModeChanged;
		Interaction.OnAreaPlacementPreviewChanged += HandleAreaPlacementPreviewChanged;
		Interaction.OnAreaPlacementConfirmed += HandleAreaPlacementConfirmed;
	}

	private void OnDestroy()
	{
		if (areaManager != null)
		{
			areaManager.OnAreaAdded -= HandleAreaListChanged;
			areaManager.OnAreaChanged -= HandleAreaListChanged;
			areaManager.OnAreaRemoved -= HandleAreaRemoved;
			areaManager.OnAreasRebuilt -= RefreshVisibleAreas;
		}

		if (GameContext.HasInstance == false || GameContext.Instance.InteractionCtx == null)
			return;

		Interaction.OnResolveSelectionFallback -= ResolveAreaSelection;
		Interaction.OnModeChanged -= HandleInteractionModeChanged;
		Interaction.OnAreaPlacementPreviewChanged -= HandleAreaPlacementPreviewChanged;
		Interaction.OnAreaPlacementConfirmed -= HandleAreaPlacementConfirmed;
	}

	public void SetAreaModeActive(bool active, AreaType areaType, int floor = 0)
	{
		isVisible = active;
		activeAreaType = areaType;
		currentFloor = floor;
		if (active == false)
		{
			if (Interaction.Mode == InteractionContext.InteractionMode.AreaEdit)
				Interaction.ExitAreaPlacementMode();

			if (Interaction.SelectedObject != null && Interaction.SelectedObject.TryGetComponent<AreaSelectionProxy>(out _))
				Interaction.ClearSelection();
		}

		RefreshOverlayState();
	}

	public void BeginCreate(AreaType areaType, int floor = 0)
	{
		SetAreaModeActive(true, areaType, floor);
		Interaction.EnterAreaPlacementMode(areaType, floor);
	}

	public AreaSelectionProxy GetSelectionProxy(Area area)
	{
		return area != null ? GetOrCreateProxy(area) : null;
	}

	private void HandleAreaListChanged(Area area)
	{
		if (isVisible)
			RefreshVisibleAreas();
	}

	private void HandleAreaRemoved(Area area)
	{
		if (area != null && proxies.TryGetValue(area, out AreaSelectionProxy proxy))
		{
			if (Interaction.SelectedObject == proxy.gameObject)
				Interaction.ClearSelection();

			Destroy(proxy.gameObject);
			proxies.Remove(area);
		}

		if (isVisible)
			RefreshVisibleAreas();
	}

	private void HandleInteractionModeChanged(InteractionContext.InteractionDomain domain, InteractionContext.InteractionAction action)
	{
		RefreshOverlayState();
	}

	private GameObject ResolveAreaSelection(int3 position)
	{
		if (isVisible == false || areaManager == null || position.y != currentFloor)
			return null;

		if (areaManager.TryGetAreaAt(position, out Area area) == false || area.Type != activeAreaType)
			return null;

		AreaSelectionProxy proxy = GetOrCreateProxy(area);
		return proxy != null ? proxy.gameObject : null;
	}

	private void HandleAreaPlacementConfirmed(AreaType areaType, RectInt bounds, int floor)
	{
		if (isVisible == false || areaManager == null || areaType != activeAreaType || floor != currentFloor)
			return;

		Area createdArea = areaManager.AddArea(areaType, bounds, floor);
		if (createdArea == null)
			return;

		RefreshVisibleAreas();
		Interaction.ExitAreaPlacementMode();
		Interaction.ClearSelection();
	}

	private void HandleAreaPlacementPreviewChanged(InteractionContext.AreaPlacementPreview preview)
	{
		if (isVisible == false
			|| Interaction.Mode != InteractionContext.InteractionMode.AreaEdit
			|| preview.AreaType != activeAreaType
			|| preview.HasStart == false)
		{
			HidePreview();
			return;
		}

		RectInt bounds = BuildRect(preview.Start, preview.End);
		bool canPlace = areaManager != null && areaManager.CanPlaceArea(preview.Floor, bounds);
		Color color = canPlace ? areaManager.GetAreaColor(preview.AreaType) : invalidPreviewColor;
		color.a = canPlace ? overlayAlpha : invalidPreviewColor.a;

		previewQuad.SetActive(true);
		previewLabel.SetActive(true);
		ConfigureQuad(previewQuad, bounds, color, previewHeight);
		ConfigureLabel(previewLabel, bounds, $"{preview.AreaType}\n{bounds.width} x {bounds.height}", color);
	}

	private void RefreshVisibleAreas()
	{
		HideAllVisuals();
		if (isVisible == false || areaManager == null)
			return;

		IReadOnlyList<Area> registeredAreas = areaManager.RegisteredAreas;
		for (int i = 0; i < registeredAreas.Count; ++i)
		{
			Area area = registeredAreas[i];
			if (area == null || area.Floor != currentFloor || area.Type != activeAreaType)
				continue;

			GameObject quad = quadPool.Get();
			GameObject label = labelPool.Get();
			AreaSelectionProxy proxy = GetOrCreateProxy(area);
			Color color = areaManager.GetAreaColor(area.Type);
			color.a = overlayAlpha;
			ConfigureQuad(quad, area.Bounds, color, areaOverlayHeight);
			ConfigureLabel(label, area.Bounds, area.DisplayName, color);
			activeVisuals[area] = new AreaVisual { Quad = quad, Label = label, Proxy = proxy };
		}
	}

	private void RefreshOverlayState()
	{
		overlayRoot?.SetActive(isVisible);
		previewRoot?.SetActive(isVisible);
		if (isVisible)
			RefreshVisibleAreas();
		else
		{
			HideAllVisuals();
			HidePreview();
		}
	}

	private AreaSelectionProxy GetOrCreateProxy(Area area)
	{
		if (proxies.TryGetValue(area, out AreaSelectionProxy proxy) && proxy != null)
		{
			proxy.Bind(areaManager, area);
			return proxy;
		}

		if (selectionProxyPrefab == null)
		{
			Debug.LogError("[AreaOverlayController] AreaSelectionProxy prefab is missing.", this);
			return null;
		}

		proxy = Instantiate(selectionProxyPrefab, proxyRoot.transform);
		proxy.name = $"AreaSelection_{area.DisplayName}";
		proxy.gameObject.hideFlags = HideFlags.HideInHierarchy;
		proxy.Bind(areaManager, area);
		proxies[area] = proxy;
		return proxy;
	}

	private GameObject CreateRoot(string objectName)
	{
		GameObject root = new(objectName);
		root.transform.SetParent(transform, false);
		return root;
	}

	private GameObject CreateQuad(string objectName, Transform parent)
	{
		if (overlayQuadPrefab == null)
		{
			Debug.LogError("[AreaOverlayController] Overlay quad prefab is missing.", this);
			return null;
		}

		GameObject quad = Instantiate(overlayQuadPrefab, parent);
		quad.name = objectName;
		return quad;
	}

	private GameObject CreateLabel(string objectName, Transform parent)
	{
		if (overlayLabelPrefab == null)
		{
			Debug.LogError("[AreaOverlayController] Overlay label prefab is missing.", this);
			return null;
		}

		GameObject label = Instantiate(overlayLabelPrefab, parent);
		label.name = objectName;
		TextMeshPro text = label.GetComponent<TextMeshPro>();
		text.fontSize = 5f;
		text.color = Color.white;
		return label;
	}

	private void HideAllVisuals()
	{
		quadPool?.ReleaseAll();
		labelPool?.ReleaseAll();
		activeVisuals.Clear();
	}

	private void HidePreview()
	{
		previewQuad?.SetActive(false);
		previewLabel?.SetActive(false);
	}

	private static RectInt BuildRect(in int3 start, in int3 end)
	{
		int minX = Mathf.Min(start.x, end.x);
		int minZ = Mathf.Min(start.z, end.z);
		int maxX = Mathf.Max(start.x, end.x);
		int maxZ = Mathf.Max(start.z, end.z);
		return new RectInt(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
	}

	private static void ConfigureQuad(GameObject quad, RectInt bounds, Color color, float height)
	{
		quad.transform.position = new Vector3(
			bounds.xMin + bounds.width * 0.5f - 0.5f,
			height,
			bounds.yMin + bounds.height * 0.5f - 0.5f);
		quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		quad.transform.localScale = new Vector3(bounds.width, bounds.height, 1f);
		quad.GetComponent<MeshRenderer>().material.color = color;
	}

	private void ConfigureLabel(GameObject label, RectInt bounds, string value, Color areaColor)
	{
		TextMeshPro text = label.GetComponent<TextMeshPro>();
		text.text = value;
		text.color = GetReadableTextColor(areaColor);
		label.transform.position = new Vector3(
			bounds.xMin + bounds.width * 0.5f - 0.5f,
			labelHeight,
			bounds.yMin + bounds.height * 0.5f - 0.5f);
		label.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
		float scale = Mathf.Clamp(Mathf.Min(bounds.width, bounds.height) / 3f, 0.35f, 1.5f);
		label.transform.localScale = Vector3.one * scale;
	}

	private static Color GetReadableTextColor(Color background)
	{
		float luminance = background.r * 0.299f + background.g * 0.587f + background.b * 0.114f;
		return luminance > 0.55f ? Color.black : Color.white;
	}
}
