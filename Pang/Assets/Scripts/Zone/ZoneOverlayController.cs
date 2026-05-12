using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

public class ZoneOverlayController : MonoBehaviour
{
	private sealed class ZoneVisual
	{
		public GameObject Quad;
		public GameObject Label;
		public ZoneSelectionProxy Proxy;
	}

	[SerializeField] private ZoneManager zoneManager;
	[SerializeField] private int previewPoolSize = 8;
	[SerializeField] private float zoneOverlayHeight = 0.02f;
	[SerializeField] private float labelHeight = 0.03f;
	[SerializeField] private float previewHeight = 0.035f;
	[SerializeField] private float overlayAlpha = 0.25f;
	[SerializeField] private Color invalidPreviewColor = new(1f, 0.25f, 0.25f, 0.4f);
	[SerializeField] private int currentFloor = 0;

	private readonly Dictionary<ZoneArea, ZoneSelectionProxy> proxies = new();
	private readonly Dictionary<ZoneArea, ZoneVisual> activeVisuals = new();

	private GameObjectPool quadPool;
	private GameObjectPool labelPool;
	private GameObject overlayRoot;
	private GameObject previewRoot;
	private GameObject proxyRoot;
	private GameObject previewQuad;
	private GameObject previewLabel;
	private bool isVisible;

	private InteractionContext Interaction => GameContext.Instance.InteractionCtx;

	private void Awake()
	{
		if (zoneManager == null)
			zoneManager = GetComponent<ZoneManager>();

		overlayRoot = new GameObject("ZoneOverlayRoot");
		previewRoot = new GameObject("ZonePreviewRoot");
		proxyRoot = new GameObject("ZoneProxyRoot");

		overlayRoot.transform.SetParent(transform, false);
		previewRoot.transform.SetParent(transform, false);
		proxyRoot.transform.SetParent(transform, false);

		quadPool = new GameObjectPool(previewPoolSize, () => CreateQuad("ZoneOverlayQuad", overlayRoot.transform));
		labelPool = new GameObjectPool(previewPoolSize, () => CreateLabel("ZoneOverlayLabel", overlayRoot.transform));

		previewQuad = CreateQuad("ZonePreviewQuad", previewRoot.transform);
		previewLabel = CreateLabel("ZonePreviewLabel", previewRoot.transform);
		previewQuad.SetActive(false);
		previewLabel.SetActive(false);

		overlayRoot.SetActive(false);
		previewRoot.SetActive(false);
		proxyRoot.SetActive(true);

		if (zoneManager != null)
		{
			zoneManager.OnZoneAdded += HandleZoneListChanged;
			zoneManager.OnZoneChanged += HandleZoneListChanged;
			zoneManager.OnZonesRebuilt += RefreshVisibleZones;
			zoneManager.OnZoneRemoved += HandleZoneRemoved;
		}

		Interaction.OnResolveSelectionFallback += ResolveZoneSelection;
		Interaction.OnZonePlacementPreviewChanged += HandleZonePlacementPreviewChanged;
		Interaction.OnZonePlacementConfirmed += HandleZonePlacementConfirmed;
	}

	private void OnDestroy()
	{
		if (zoneManager != null)
		{
			zoneManager.OnZoneAdded -= HandleZoneListChanged;
			zoneManager.OnZoneChanged -= HandleZoneListChanged;
			zoneManager.OnZonesRebuilt -= RefreshVisibleZones;
			zoneManager.OnZoneRemoved -= HandleZoneRemoved;
		}

		if (GameContext.HasInstance && GameContext.Instance.InteractionCtx != null)
		{
			Interaction.OnResolveSelectionFallback -= ResolveZoneSelection;
			Interaction.OnZonePlacementPreviewChanged -= HandleZonePlacementPreviewChanged;
			Interaction.OnZonePlacementConfirmed -= HandleZonePlacementConfirmed;
		}
	}

	public void SetOverlayVisible(bool visible)
	{
		if (isVisible == visible)
			return;

		isVisible = visible;
		overlayRoot.SetActive(visible);
		previewRoot.SetActive(visible);

		if (visible)
		{
			RefreshVisibleZones();
		}
		else
		{
			HideAllVisuals();
			HidePreview();

			if (Interaction.Mode == InteractionContext.InteractionMode.ZonePlacement)
				Interaction.ExitZonePlacementMode();

			if (Interaction.SelectedObject != null && Interaction.SelectedObject.TryGetComponent<ZoneSelectionProxy>(out _))
				Interaction.ClearSelection();
		}
	}

	public void BeginCreate(ZoneType zoneType)
	{
		SetOverlayVisible(true);
		Interaction.EnterZonePlacementMode(zoneType, currentFloor);
	}

	private void HandleZoneListChanged(ZoneArea zone)
	{
		if (isVisible)
			RefreshVisibleZones();
	}

	private void HandleZoneRemoved(ZoneArea zone)
	{
		if (zone != null && proxies.TryGetValue(zone, out var proxy))
		{
			if (Interaction.SelectedObject == proxy.gameObject)
				Interaction.ClearSelection();

			Destroy(proxy.gameObject);
			proxies.Remove(zone);
		}

		if (isVisible)
			RefreshVisibleZones();
	}

	private GameObject ResolveZoneSelection(int3 pos)
	{
		if (isVisible == false || zoneManager == null || pos.y != currentFloor)
			return null;

		if (zoneManager.TryGetZoneAt(pos, out var zone) == false)
			return null;

		return GetOrCreateProxy(zone).gameObject;
	}

	private void HandleZonePlacementConfirmed(ZoneType zoneType, RectInt bound, int floor)
	{
		if (zoneManager == null || floor != currentFloor)
			return;

		if (zoneManager.CanPlaceZone(floor, bound) == false)
		{
			Debug.LogWarning($"Cannot create zone {zoneType}: overlapped bounds {bound}");
			return;
		}

		var createdZone = zoneManager.AddZone(zoneType, bound, floor);
		if (createdZone == null)
			return;

		RefreshVisibleZones();
		Interaction.ExitZonePlacementMode();
		Interaction.ClearSelection();
	}

	private void HandleZonePlacementPreviewChanged(InteractionContext.ZonePlacementPreview preview)
	{
		if (isVisible == false || Interaction.Mode != InteractionContext.InteractionMode.ZonePlacement || preview.HasStart == false)
		{
			HidePreview();
			return;
		}

		var bound = BuildRect(preview.Start, preview.End);
		bool canPlace = zoneManager != null && zoneManager.CanPlaceZone(preview.Floor, bound);
		Color color = canPlace ? zoneManager.GetZoneColor(preview.ZoneType) : invalidPreviewColor;
		color.a = canPlace ? overlayAlpha : invalidPreviewColor.a;

		previewQuad.SetActive(true);
		previewLabel.SetActive(true);
		ConfigureQuad(previewQuad, bound, color, previewHeight);
		ConfigureLabel(
			previewLabel,
			bound,
			$"{preview.ZoneType}\n{bound.width} x {bound.height}",
			color
		);
	}

	private void RefreshVisibleZones()
	{
		if (isVisible == false || zoneManager == null)
			return;

		HideAllVisuals();

		foreach (var zone in zoneManager.RegisteredZones)
		{
			if (zone == null || zone.Floor != currentFloor)
				continue;

			GameObject quad = quadPool.Get();
			GameObject label = labelPool.Get();
			ZoneSelectionProxy proxy = GetOrCreateProxy(zone);
			Color zoneColor = zoneManager.GetZoneColor(zone.Type);
			zoneColor.a = overlayAlpha;

			ConfigureQuad(quad, zone.Bounds, zoneColor, zoneOverlayHeight);
			ConfigureLabel(label, zone.Bounds, zone.DisplayName, zoneColor);

			activeVisuals[zone] = new ZoneVisual
			{
				Quad = quad,
				Label = label,
				Proxy = proxy,
			};
		}
	}

	private void HideAllVisuals()
	{
		quadPool?.ReleaseAll();
		labelPool?.ReleaseAll();
		activeVisuals.Clear();
	}

	private void HidePreview()
	{
		if (previewQuad != null)
			previewQuad.SetActive(false);

		if (previewLabel != null)
			previewLabel.SetActive(false);
	}

	private ZoneSelectionProxy GetOrCreateProxy(ZoneArea zone)
	{
		if (proxies.TryGetValue(zone, out var proxy) && proxy != null)
		{
			proxy.Bind(zoneManager, zone);
			return proxy;
		}

		GameObject proxyObject = new($"ZoneSelection_{zone.DisplayName}");
		proxyObject.transform.SetParent(proxyRoot.transform, false);
		proxyObject.hideFlags = HideFlags.HideInHierarchy;

		proxy = proxyObject.AddComponent<ZoneSelectionProxy>();
		proxy.Bind(zoneManager, zone);
		proxies[zone] = proxy;
		return proxy;
	}

	private GameObject CreateQuad(string objectName, Transform parent)
	{
		GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
		quad.name = objectName;
		quad.transform.SetParent(parent, false);

		var collider = quad.GetComponent<Collider>();
		if (collider != null)
			Destroy(collider);

		var renderer = quad.GetComponent<MeshRenderer>();
		renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		renderer.receiveShadows = false;
		renderer.material = CreateOverlayMaterial();

		return quad;
	}

	private GameObject CreateLabel(string objectName, Transform parent)
	{
		GameObject label = new(objectName);
		label.transform.SetParent(parent, false);

		var text = label.AddComponent<TextMeshPro>();
		text.alignment = TextAlignmentOptions.Center;
		text.fontSize = 5f;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.color = Color.white;

		return label;
	}

	private void ConfigureQuad(GameObject quad, RectInt bounds, Color color, float height)
	{
		quad.transform.position = new Vector3(
			bounds.xMin + (bounds.width * 0.5f) - 0.5f,
			height,
			bounds.yMin + (bounds.height * 0.5f) - 0.5f
		);
		quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		quad.transform.localScale = new Vector3(bounds.width, bounds.height, 1f);

		var renderer = quad.GetComponent<MeshRenderer>();
		renderer.material.color = color;
	}

	private void ConfigureLabel(GameObject label, RectInt bounds, string textValue, Color zoneColor)
	{
		var text = label.GetComponent<TextMeshPro>();
		text.text = textValue;
		text.color = GetReadableTextColor(zoneColor);

		label.transform.position = new Vector3(
			bounds.xMin + (bounds.width * 0.5f) - 0.5f,
			labelHeight,
			bounds.yMin + (bounds.height * 0.5f) - 0.5f
		);
		label.transform.rotation = Quaternion.Euler(90f, 180f, 0f);

		float scale = Mathf.Clamp(Mathf.Min(bounds.width, bounds.height) / 3f, 0.35f, 1.5f);
		label.transform.localScale = Vector3.one * scale;
	}

	private Material CreateOverlayMaterial()
	{
		Shader shader = Shader.Find("Sprites/Default");
		if (shader == null)
			shader = Shader.Find("Unlit/Color");

		Material material = new(shader);
		material.renderQueue = 3000;
		return material;
	}

	private static RectInt BuildRect(in int3 start, in int3 end)
	{
		int minX = Mathf.Min(start.x, end.x);
		int minZ = Mathf.Min(start.z, end.z);
		int maxX = Mathf.Max(start.x, end.x);
		int maxZ = Mathf.Max(start.z, end.z);
		return new RectInt(minX, minZ, (maxX - minX) + 1, (maxZ - minZ) + 1);
	}

	private static Color GetReadableTextColor(Color background)
	{
		float luminance = (background.r * 0.299f) + (background.g * 0.587f) + (background.b * 0.114f);
		return luminance > 0.55f ? Color.black : Color.white;
	}
}
