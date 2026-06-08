using TMPro;
using Unity.Mathematics;
using UnityEngine;

public sealed class BuildingPlacementOverlayController : MonoBehaviour
{
	[SerializeField] private BuildingFootprintService footprintService;
	[SerializeField] private float previewHeight = 0.035f;
	[SerializeField] private float labelHeight = 0.04f;
	[SerializeField] private float overlayAlpha = 0.25f;
	[SerializeField] private Color previewColor = new(0.2f, 0.7f, 0.85f, 0.4f);
	[SerializeField] private Color invalidPreviewColor = new(1f, 0.25f, 0.25f, 0.4f);
	[SerializeField] private int currentFloor = 0;

	private GameObject previewRoot;
	private GameObject previewQuad;
	private GameObject previewLabel;
	private bool isVisible;

	private InteractionContext Interaction => GameContext.Instance.InteractionCtx;
	private BuildingFootprintService FootprintService
	{
		get
		{
			if (footprintService == null && GameContext.HasInstance)
				footprintService = GameContext.Instance.BuildingFootprintService;

			return footprintService;
		}
	}

	private void Awake()
	{
		previewRoot = new GameObject("BuildingPreviewRoot");
		Transform worldParent = GameContext.HasInstance ? GameContext.Instance.transform : null;
		previewRoot.transform.SetParent(worldParent, false);
		previewRoot.transform.localScale = Vector3.one;

		previewQuad = CreateQuad("BuildingPreviewQuad", previewRoot.transform);
		previewLabel = CreateLabel("BuildingPreviewLabel", previewRoot.transform);
		previewQuad.SetActive(false);
		previewLabel.SetActive(false);
		previewRoot.SetActive(false);

		Interaction.OnBuildingPlacementPreviewChanged += HandleBuildingPlacementPreviewChanged;
		Interaction.OnBuildingPlacementConfirmed += HandleBuildingPlacementConfirmed;
	}

	private void OnDestroy()
	{
		if (GameContext.HasInstance == false || GameContext.Instance.InteractionCtx == null)
			return;

		Interaction.OnBuildingPlacementPreviewChanged -= HandleBuildingPlacementPreviewChanged;
		Interaction.OnBuildingPlacementConfirmed -= HandleBuildingPlacementConfirmed;
	}

	public void SetOverlayVisible(bool visible)
	{
		isVisible = visible;
		if (previewRoot != null)
			previewRoot.SetActive(visible);

		if (visible == false)
		{
			HidePreview();
			if (Interaction.Mode == InteractionContext.InteractionMode.BuildingPlacement)
				Interaction.ExitBuildingPlacementMode();
		}
	}

	public void BeginCreate()
	{
		SetOverlayVisible(true);
		Interaction.EnterBuildingPlacementMode(currentFloor);
	}

	private void HandleBuildingPlacementPreviewChanged(InteractionContext.BuildingPlacementPreview preview)
	{
		if (isVisible == false || Interaction.Mode != InteractionContext.InteractionMode.BuildingPlacement || preview.HasStart == false)
		{
			HidePreview();
			return;
		}

		RectInt bounds = BuildRect(preview.Start, preview.End);
		bool canCreate = FootprintService != null && FootprintService.CanCreateFootprint(preview.Floor, bounds, out _);
		Color color = canCreate ? previewColor : invalidPreviewColor;
		color.a = canCreate ? overlayAlpha : invalidPreviewColor.a;

		previewQuad.SetActive(true);
		previewLabel.SetActive(true);
		ConfigureQuad(previewQuad, bounds, color);
		ConfigureLabel(previewLabel, bounds, $"{bounds.width} x {bounds.height}", color);
	}

	private void HandleBuildingPlacementConfirmed(RectInt bounds, int floor)
	{
		if (FootprintService == null)
			return;

		if (FootprintService.TryCreateFootprint(floor, bounds, out string reason) == false)
		{
			if (string.IsNullOrWhiteSpace(reason) == false)
				Debug.LogWarning(reason);
			return;
		}

		Interaction.ExitBuildingPlacementMode();
		Interaction.ClearSelection();
	}

	private void HidePreview()
	{
		if (previewQuad != null)
			previewQuad.SetActive(false);

		if (previewLabel != null)
			previewLabel.SetActive(false);
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

	private void ConfigureQuad(GameObject quad, RectInt bounds, Color color)
	{
		quad.transform.position = new Vector3(
			bounds.xMin + (bounds.width * 0.5f) - 0.5f,
			previewHeight,
			bounds.yMin + (bounds.height * 0.5f) - 0.5f
		);
		quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		quad.transform.localScale = new Vector3(bounds.width, bounds.height, 1f);

		var renderer = quad.GetComponent<MeshRenderer>();
		renderer.material.color = color;
	}

	private void ConfigureLabel(GameObject label, RectInt bounds, string textValue, Color backgroundColor)
	{
		var text = label.GetComponent<TextMeshPro>();
		text.text = textValue;
		text.color = GetReadableTextColor(backgroundColor);

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
