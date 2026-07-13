using Unity.Mathematics;
using UnityEngine;

public sealed class GridOverlayController : MonoBehaviour
{
	private static readonly int GridTextureId = Shader.PropertyToID("_GridTex");
	private static readonly int UseDirectColorId = Shader.PropertyToID("_UseDirectColor");
	private static readonly int OverlayAlphaId = Shader.PropertyToID("_OverlayAlpha");
	private static readonly int HideZeroAlphaPixelsId = Shader.PropertyToID("_HideZeroAlphaPixels");

	[SerializeField, Range(0f, 1f)] private float overlayAlpha = 0.45f;
	[SerializeField] private float overlayHeight = 0.03f;
	[SerializeField, Min(0)] private int floor;

	private IGridOverlayProvider provider;
	private Color32[] colorBuffer;
	private Texture2D texture;
	private Material material;
	private GameObject overlayQuad;
	private bool isHolding;
	private KeyCode activeKey = KeyCode.None;

	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Keypad1))
			BeginHold(KeyCode.Keypad1, GameContext.Instance.TemperatureSvc);

		if (Input.GetKeyDown(KeyCode.Keypad2))
			BeginHold(KeyCode.Keypad2, GameContext.Instance.FacilityRuleMgr);

		if (isHolding && Input.GetKey(activeKey) == false)
			EndHold();
	}

	private void OnDisable()
	{
		EndHold();
	}

	private void OnDestroy()
	{
		EndHold();

		if (overlayQuad != null)
			Destroy(overlayQuad);
		if (texture != null)
			Destroy(texture);
		if (material != null)
			Destroy(material);
	}

	private void BeginHold(KeyCode key, IGridOverlayProvider nextProvider)
	{
		if (GridService == null || GridService.IsReady == false || nextProvider == null)
			return;

		EndHold();
		provider = nextProvider;
		if (provider == null || EnsureRenderResources() == false)
		{
			provider = null;
			return;
		}

		provider.OnGridOverlayRefreshRequested += HandleProviderRefreshRequested;
		material.SetFloat(HideZeroAlphaPixelsId, provider.HideZeroAlphaPixels ? 1f : 0f);
		activeKey = key;
		isHolding = true;
		RefreshTexture();
		overlayQuad.SetActive(true);
	}

	private void EndHold()
	{
		if (provider != null)
			provider.OnGridOverlayRefreshRequested -= HandleProviderRefreshRequested;

		provider = null;
		activeKey = KeyCode.None;
		isHolding = false;
		if (overlayQuad != null)
			overlayQuad.SetActive(false);
	}

	private bool EnsureRenderResources()
	{
		int3 mapSize = GridService.MapSize;
		if (mapSize.x <= 0 || mapSize.z <= 0 || floor < 0 || floor >= mapSize.y)
			return false;

		int requiredLength = mapSize.x * mapSize.z;
		if (colorBuffer == null || colorBuffer.Length != requiredLength)
			colorBuffer = new Color32[requiredLength];

		if (texture == null || texture.width != mapSize.x || texture.height != mapSize.z)
		{
			if (texture != null)
				Destroy(texture);

			texture = new Texture2D(mapSize.x, mapSize.z, TextureFormat.RGBA32, false, true)
			{
				name = "GridOverlayTexture",
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
			};
		}

		if (material == null)
		{
			Shader shader = Shader.Find("Custom/GridBoundaryShader");
			if (shader == null)
			{
				Debug.LogError("[GridOverlayController] Custom/GridBoundaryShader could not be found.", this);
				return false;
			}

			material = new Material(shader)
			{
				name = "GridOverlayMaterial (Runtime)",
			};
			material.SetFloat(UseDirectColorId, 1f);
			material.SetFloat(OverlayAlphaId, overlayAlpha);
		}

		if (overlayQuad == null)
		{
			overlayQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
			overlayQuad.name = "GridOverlayQuad";
			overlayQuad.transform.SetParent(transform, false);

			Collider collider = overlayQuad.GetComponent<Collider>();
			if (collider != null)
				Destroy(collider);
		}

		overlayQuad.transform.position = new Vector3(
			mapSize.x * 0.5f - 0.5f,
			overlayHeight,
			mapSize.z * 0.5f - 0.5f);
		overlayQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		overlayQuad.transform.localScale = new Vector3(mapSize.x, mapSize.z, 1f);

		MeshRenderer renderer = overlayQuad.GetComponent<MeshRenderer>();
		renderer.sharedMaterial = material;
		material.SetTexture(GridTextureId, texture);
		overlayQuad.SetActive(false);
		return true;
	}

	private void HandleProviderRefreshRequested()
	{
		if (isHolding)
			RefreshTexture();
	}

	private void RefreshTexture()
	{
		if (provider == null || texture == null || provider.TryFillGridOverlay(colorBuffer, floor) == false)
			return;

		texture.SetPixelData(colorBuffer, 0, 0);
		texture.Apply(false, false);
	}
}
