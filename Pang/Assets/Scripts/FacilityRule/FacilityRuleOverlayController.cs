using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class FacilityRuleOverlayController : MonoBehaviour
{
	private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");

	[SerializeField] private FacilityRuleManager ruleManager = null;
	[SerializeField] private FacilityManager facilityManager = null;
	[SerializeField] private GridService gridService = null;
	[SerializeField] private GameObject coloredFloorPrefab = null;
	[SerializeField] private int preloadCount = 32;
	[SerializeField] private float overlayHeight = 0.025f;
	[SerializeField, Range(0f, 1f)] private float glowAlpha = 1f;

	private MaterialPropertyBlock propertyBlock;
	private GameObjectPool tilePool;
	private GameObject overlayRoot;
	[System.NonSerialized] private bool initialized;

	private void Awake()
	{
		EnsureInitialized();
	}

	private void OnEnable()
	{
		EnsureInitialized();
		Subscribe();
		RefreshOverlay();
	}

	private void OnDisable()
	{
		Unsubscribe();
		ClearOverlay();
	}

	private void OnDestroy()
	{
		Unsubscribe();
	}

	public void RefreshOverlay()
	{
		EnsureInitialized();
		ClearOverlay();

		if (ruleManager == null || gridService == null || coloredFloorPrefab == null)
			return;

		ruleManager.RebuildAppliedFacilityLookup();

		IReadOnlyList<FacilityRulePreset> presets = ruleManager.Presets;
		if (presets == null)
			return;

		for (int i = 0; i < presets.Count; ++i)
		{
			FacilityRulePreset preset = presets[i];
			if (preset == null || preset.Id == FacilityRuleManager.NoRulePresetId)
				continue;

			IReadOnlyList<IFacility> facilities = ruleManager.GetFacilitiesForPreset(preset.Id);
			for (int facilityIndex = 0; facilityIndex < facilities.Count; ++facilityIndex)
				DrawFacility(facilities[facilityIndex], preset.Color);
		}
	}

	private void EnsureInitialized()
	{
		if (initialized)
			return;

		if (overlayRoot != null && tilePool == null)
		{
			Destroy(overlayRoot);
			overlayRoot = null;
		}

		if (overlayRoot == null)
		{
			overlayRoot = new GameObject("FacilityRuleOverlayRoot");
			overlayRoot.transform.SetParent(transform, false);
			overlayRoot.transform.localScale = Vector3.one;
		}

		propertyBlock ??= new MaterialPropertyBlock();

		if (tilePool == null && coloredFloorPrefab != null)
			tilePool = new GameObjectPool(preloadCount, CreateTile);

		initialized = true;
	}

	private void Subscribe()
	{
		if (ruleManager != null)
		{
			ruleManager.OnPresetCreated -= HandlePresetChanged;
			ruleManager.OnPresetChanged -= HandlePresetChanged;
			ruleManager.OnPresetDeleted -= HandlePresetDeleted;
			ruleManager.OnFacilityRulePresetApplied -= HandleFacilityRulePresetApplied;
			ruleManager.OnPresetsRebuilt -= RefreshOverlay;

			ruleManager.OnPresetCreated += HandlePresetChanged;
			ruleManager.OnPresetChanged += HandlePresetChanged;
			ruleManager.OnPresetDeleted += HandlePresetDeleted;
			ruleManager.OnFacilityRulePresetApplied += HandleFacilityRulePresetApplied;
			ruleManager.OnPresetsRebuilt += RefreshOverlay;
		}

		if (facilityManager != null)
		{
			facilityManager.UnsubscribeFacilityRegister<IFacility>(HandleFacilityRegistered, HandleFacilityUnregistered);
			facilityManager.SubscribeFacilityRegister<IFacility>(HandleFacilityRegistered, HandleFacilityUnregistered);
		}
	}

	private void Unsubscribe()
	{
		if (ruleManager != null)
		{
			ruleManager.OnPresetCreated -= HandlePresetChanged;
			ruleManager.OnPresetChanged -= HandlePresetChanged;
			ruleManager.OnPresetDeleted -= HandlePresetDeleted;
			ruleManager.OnFacilityRulePresetApplied -= HandleFacilityRulePresetApplied;
			ruleManager.OnPresetsRebuilt -= RefreshOverlay;
		}

		if (facilityManager != null)
			facilityManager.UnsubscribeFacilityRegister<IFacility>(HandleFacilityRegistered, HandleFacilityUnregistered);
	}

	private void DrawFacility(IFacility facility, Color color)
	{
		if (facility == null || tilePool == null)
			return;

		if (TryDrawFacilityFootprint(facility, color))
			return;

		DrawTile(facility.GridPosition, color);
	}

	private bool TryDrawFacilityFootprint(IFacility facility, Color color)
	{
		if (facility is not Component component || component == null || gridService == null)
			return false;

		GameObject targetObject = component.gameObject;
		int3 mapSize = gridService.MapSize;
		bool drewAny = false;

		for (int y = 0; y < mapSize.y; ++y)
		{
			for (int x = 0; x < mapSize.x; ++x)
			{
				for (int z = 0; z < mapSize.z; ++z)
				{
					GridCell cell = gridService.GetCell(x, y, z);
					if (cell == null || cell.OccupancyObjectOnGrid != targetObject)
						continue;

					DrawTile(new int3(x, y, z), color);
					drewAny = true;
				}
			}
		}

		return drewAny;
	}

	private void DrawTile(in int3 gridPosition, Color color)
	{
		GameObject tile = tilePool.Get();
		if (tile == null)
			return;

		tile.transform.position = new Vector3(gridPosition.x, gridPosition.y + overlayHeight, gridPosition.z);
		tile.transform.localScale = Vector3.one;
		SetTileColor(tile, color);
	}

	private GameObject CreateTile()
	{
		if (coloredFloorPrefab == null || overlayRoot == null)
			return null;

		GameObject tile = Instantiate(coloredFloorPrefab, overlayRoot.transform);
		tile.name = "FacilityRuleColoredFloor";

		Collider[] colliders = tile.GetComponentsInChildren<Collider>(true);
		for (int i = 0; i < colliders.Length; ++i)
			colliders[i].enabled = false;

		return tile;
	}

	private void SetTileColor(GameObject tile, Color color)
	{
		propertyBlock ??= new MaterialPropertyBlock();
		color.a = glowAlpha;

		Renderer[] renderers = tile.GetComponentsInChildren<Renderer>(true);
		for (int i = 0; i < renderers.Length; ++i)
		{
			Renderer renderer = renderers[i];
			renderer.GetPropertyBlock(propertyBlock);
			propertyBlock.SetColor(GlowColorId, color);
			renderer.SetPropertyBlock(propertyBlock);
			propertyBlock.Clear();
		}
	}

	private void ClearOverlay()
	{
		tilePool?.ReleaseAll();
	}

	private void HandlePresetChanged(FacilityRulePreset preset)
	{
		RefreshOverlay();
	}

	private void HandlePresetDeleted(uint presetId)
	{
		RefreshOverlay();
	}

	private void HandleFacilityRulePresetApplied(IFacility facility, uint previousPresetId, uint nextPresetId)
	{
		RefreshOverlay();
	}

	private void HandleFacilityRegistered(uint buildingId, IFacility facility)
	{
		RefreshOverlay();
	}

	private void HandleFacilityUnregistered(uint buildingId, IFacility facility)
	{
		RefreshOverlay();
	}
}
