using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class FacilityRuleManager : MonoBehaviour, IGridOverlayProvider
{
	public const uint NoRulePresetId = 0;

	[SerializeField] private uint nextPresetId = 1;
	[SerializeField] private List<FacilityRulePreset> presets = new();

	private readonly Dictionary<uint, FacilityRulePreset> presetsById = new();
	private readonly Dictionary<uint, List<IFacility>> facilitiesByPresetId = new();
	private readonly Dictionary<GameObject, IFacility> facilitiesByObject = new();
	private static readonly IReadOnlyList<IFacility> EmptyFacilities = Array.Empty<IFacility>();
	private ItemDefinition gridOverlayItemFilter;

	public IReadOnlyList<FacilityRulePreset> Presets => presets;
	public bool HasGridOverlayItemFilter => gridOverlayItemFilter != null;
	public ItemDefinition GridOverlayItemFilter => gridOverlayItemFilter;

	public event Action<FacilityRulePreset> OnPresetCreated;
	public event Action<FacilityRulePreset> OnPresetChanged;
	public event Action<uint> OnPresetDeleted;
	public event Action<IFacility, uint, uint> OnFacilityRulePresetApplied;
	public event Action OnPresetsRebuilt;
	public event Action OnGridOverlayRefreshRequested;
	public bool HideZeroAlphaPixels => true;

	private void Awake()
	{
		RebuildPresetLookup();
	}

	private void Start()
	{
		if (GameContext.HasInstance && GameContext.Instance.FacilityMgr != null)
		{
			GameContext.Instance.FacilityMgr.SubscribeFacilityRegister<IFacility>(
				HandleFacilityRegistered,
				HandleFacilityUnregistered);
		}

		RebuildAppliedFacilityLookup();
	}

	private void OnDestroy()
	{
		if (GameContext.HasInstance && GameContext.Instance.FacilityMgr != null)
		{
			GameContext.Instance.FacilityMgr.UnsubscribeFacilityRegister<IFacility>(
				HandleFacilityRegistered,
				HandleFacilityUnregistered);
		}
	}

#if UNITY_EDITOR
	private void OnValidate()
	{
		RebuildPresetLookup();
	}
#endif

	public void RebuildPresetLookup()
	{
		presetsById.Clear();

		for (int i = presets.Count - 1; i >= 0; --i)
		{
			FacilityRulePreset preset = presets[i];
			if (preset == null || preset.Id == NoRulePresetId || presetsById.ContainsKey(preset.Id))
			{
				presets.RemoveAt(i);
				continue;
			}

			presetsById[preset.Id] = preset;
			if (preset.Id >= nextPresetId)
				nextPresetId = preset.Id + 1;
		}

		if (nextPresetId == NoRulePresetId)
			nextPresetId = 1;

		OnPresetsRebuilt?.Invoke();
		OnGridOverlayRefreshRequested?.Invoke();
	}

	public void RebuildAppliedFacilityLookup()
	{
		facilitiesByPresetId.Clear();
		if (GameContext.HasInstance == false || GameContext.Instance.FacilityMgr == null)
		{
			OnGridOverlayRefreshRequested?.Invoke();
			return;
		}

		IReadOnlyList<uint> buildingIds = GameContext.Instance.FacilityMgr.GetBuildingIds();
		for (int i = 0; i < buildingIds.Count; ++i)
		{
			IReadOnlyList<IFacility> facilities = GameContext.Instance.FacilityMgr.GetFacilities<IFacility>(buildingIds[i]);
			for (int facilityIndex = 0; facilityIndex < facilities.Count; ++facilityIndex)
				AddFacilityToAppliedLookup(facilities[facilityIndex]);
		}

		OnGridOverlayRefreshRequested?.Invoke();
	}

	public FacilityRulePreset CreatePreset(string displayName, FacilityRule rule = null, Color? color = null)
	{
		uint id = AllocatePresetId();
		FacilityRulePreset preset = new(
			id,
			string.IsNullOrWhiteSpace(displayName) ? $"Rule {id}" : displayName,
			color ?? Color.white,
			rule);

		presets.Add(preset);
		presetsById[id] = preset;
		OnPresetCreated?.Invoke(preset);
		OnGridOverlayRefreshRequested?.Invoke();
		return preset;
	}

	public bool TryGetPreset(uint presetId, out FacilityRulePreset preset)
	{
		if (presetId == NoRulePresetId)
		{
			preset = null;
			return false;
		}

		if (presetsById.TryGetValue(presetId, out preset))
			return true;

		RebuildPresetLookup();
		return presetsById.TryGetValue(presetId, out preset);
	}

	public bool RenamePreset(uint presetId, string displayName)
	{
		if (TryGetPreset(presetId, out FacilityRulePreset preset) == false)
			return false;

		preset.Rename(displayName);
		OnPresetChanged?.Invoke(preset);
		return true;
	}

	public bool SetPresetColor(uint presetId, Color color)
	{
		if (TryGetPreset(presetId, out FacilityRulePreset preset) == false)
			return false;

		preset.SetColor(color);
		OnPresetChanged?.Invoke(preset);
		OnGridOverlayRefreshRequested?.Invoke();
		return true;
	}

	public bool SetPresetRule(uint presetId, FacilityRule rule)
	{
		if (TryGetPreset(presetId, out FacilityRulePreset preset) == false)
			return false;

		preset.SetRule(rule);
		OnPresetChanged?.Invoke(preset);
		return true;
	}

	public bool DeletePreset(uint presetId)
	{
		if (presetId == NoRulePresetId || presetsById.Remove(presetId) == false)
			return false;

		ClearPresetFromAppliedFacilities(presetId);

		for (int i = presets.Count - 1; i >= 0; --i)
		{
			if (presets[i] != null && presets[i].Id == presetId)
				presets.RemoveAt(i);
		}

		OnPresetDeleted?.Invoke(presetId);
		OnGridOverlayRefreshRequested?.Invoke();
		return true;
	}

	public bool ApplyPreset(IFacility facility, uint presetId)
	{
		if (facility == null)
			return false;

		if (presetId != NoRulePresetId && TryGetPreset(presetId, out _) == false)
			return false;

		uint previousPresetId = facility.FacilityRulePresetId;
		if (previousPresetId == presetId)
		{
			AddFacilityToAppliedLookup(facility);
			return true;
		}

		facility.SetFacilityRulePresetId(presetId);
		MoveFacilityAppliedLookup(facility, previousPresetId);
		OnFacilityRulePresetApplied?.Invoke(facility, previousPresetId, presetId);
		OnGridOverlayRefreshRequested?.Invoke();
		return true;
	}

	public bool TryFillGridOverlay(Color32[] buffer, int floor)
	{
		if (GameContext.HasInstance == false || GameContext.Instance.GridService == null ||
			GameContext.Instance.GridService.IsReady == false || GameContext.Instance.FacilityMgr == null)
		{
			return false;
		}

		GridService gridService = GameContext.Instance.GridService;
		Unity.Mathematics.int3 size = gridService.MapSize;
		if (buffer == null || buffer.Length < size.x * size.z || floor < 0 || floor >= size.y)
			return false;

		RebuildFacilityObjectLookup();

		for (int z = 0; z < size.z; ++z)
		{
			for (int x = 0; x < size.x; ++x)
			{
				int index = z * size.x + x;
				buffer[index] = default;

				GridCell cell = gridService.GetCell(x, floor, z);
				GameObject occupancyObject = cell?.OccupancyObjectOnGrid;
				if (occupancyObject == null || facilitiesByObject.TryGetValue(occupancyObject, out IFacility facility) == false)
					continue;

				uint presetId = facility.FacilityRulePresetId;
				if (presetId == NoRulePresetId || presetsById.TryGetValue(presetId, out FacilityRulePreset preset) == false)
					continue;

				if (gridOverlayItemFilter != null && preset.Rule != null &&
					preset.Rule.IsItemDefinitionCapable(gridOverlayItemFilter) == false)
				{
					continue;
				}

				Color32 color = preset.Color;
				color.a = 1;
				buffer[index] = color;
			}
		}

		return true;
	}

	public void SetGridOverlayItemFilter(ItemDefinition item)
	{
		if (gridOverlayItemFilter == item)
			return;

		gridOverlayItemFilter = item;
		OnGridOverlayRefreshRequested?.Invoke();
	}

	public void ClearGridOverlayItemFilter()
	{
		SetGridOverlayItemFilter(null);
	}

	private void RebuildFacilityObjectLookup()
	{
		facilitiesByObject.Clear();
		FacilityManager facilityManager = GameContext.Instance.FacilityMgr;
		IReadOnlyList<uint> buildingIds = facilityManager.GetBuildingIds();

		for (int i = 0; i < buildingIds.Count; ++i)
		{
			IReadOnlyList<IFacility> facilities = facilityManager.GetFacilities<IFacility>(buildingIds[i]);
			for (int facilityIndex = 0; facilityIndex < facilities.Count; ++facilityIndex)
			{
				IFacility facility = facilities[facilityIndex];
				if (facility is Component component && component != null)
					facilitiesByObject[component.gameObject] = facility;
			}
		}
	}

	public IReadOnlyList<IFacility> GetFacilitiesForPreset(uint presetId)
	{
		if (presetId == NoRulePresetId)
			return EmptyFacilities;

		if (facilitiesByPresetId.TryGetValue(presetId, out List<IFacility> facilities) == false)
			return EmptyFacilities;

		return facilities;
	}

	public bool TryGetFacilitiesForPreset(uint presetId, out IReadOnlyList<IFacility> facilities)
	{
		facilities = GetFacilitiesForPreset(presetId);
		return facilities.Count > 0;
	}

	public int GetAppliedFacilityCount(uint presetId)
	{
		return GetFacilitiesForPreset(presetId).Count;
	}

	public int GetNoRuleFacilityCount()
	{
		int count = 0;
		if (GameContext.HasInstance == false || GameContext.Instance.FacilityMgr == null)
			return count;

		IReadOnlyList<uint> buildingIds = GameContext.Instance.FacilityMgr.GetBuildingIds();
		for (int i = 0; i < buildingIds.Count; ++i)
		{
			IReadOnlyList<IFacility> facilities = GameContext.Instance.FacilityMgr.GetFacilities<IFacility>(buildingIds[i]);
			for (int facilityIndex = 0; facilityIndex < facilities.Count; ++facilityIndex)
			{
				IFacility facility = facilities[facilityIndex];
				if (facility != null && facility.FacilityRulePresetId == NoRulePresetId)
					count += 1;
			}
		}

		return count;
	}

	public bool IsFacilityAllowed(IFacility facility, in FacilityFilter filter)
	{
		if (facility == null)
			return false;

		uint presetId = facility.FacilityRulePresetId;
		if (presetId == NoRulePresetId)
			return true;

		if (TryGetPreset(presetId, out FacilityRulePreset preset) == false)
			return false;

		return preset.Rule == null || preset.Rule.IsFilterCapable(filter);
	}

	public bool TryGetPresetColor(uint presetId, out Color color)
	{
		if (TryGetPreset(presetId, out FacilityRulePreset preset))
		{
			color = preset.Color;
			return true;
		}

		color = Color.white;
		return false;
	}

	private uint AllocatePresetId()
	{
		if (nextPresetId == NoRulePresetId)
			nextPresetId = 1;

		while (presetsById.ContainsKey(nextPresetId))
			nextPresetId += 1;

		uint id = nextPresetId;
		nextPresetId += 1;
		return id;
	}

	private void HandleFacilityRegistered(uint buildingId, IFacility facility)
	{
		AddFacilityToAppliedLookup(facility);
		OnGridOverlayRefreshRequested?.Invoke();
	}

	private void HandleFacilityUnregistered(uint buildingId, IFacility facility)
	{
		RemoveFacilityFromAppliedLookup(facility);
		OnGridOverlayRefreshRequested?.Invoke();
	}

	private void AddFacilityToAppliedLookup(IFacility facility)
	{
		if (facility == null || facility.FacilityRulePresetId == NoRulePresetId)
			return;

		if (facilitiesByPresetId.TryGetValue(facility.FacilityRulePresetId, out List<IFacility> facilities) == false)
		{
			facilities = new List<IFacility>();
			facilitiesByPresetId[facility.FacilityRulePresetId] = facilities;
		}

		if (facilities.Contains(facility) == false)
			facilities.Add(facility);
	}

	private void RemoveFacilityFromAppliedLookup(IFacility facility)
	{
		if (facility == null || facility.FacilityRulePresetId == NoRulePresetId)
			return;

		RemoveFacilityFromAppliedLookup(facility, facility.FacilityRulePresetId);
	}

	private void RemoveFacilityFromAppliedLookup(IFacility facility, uint presetId)
	{
		if (facility == null || presetId == NoRulePresetId)
			return;

		if (facilitiesByPresetId.TryGetValue(presetId, out List<IFacility> facilities) == false)
			return;

		facilities.Remove(facility);
		if (facilities.Count <= 0)
			facilitiesByPresetId.Remove(presetId);
	}

	private void MoveFacilityAppliedLookup(IFacility facility, uint previousPresetId)
	{
		RemoveFacilityFromAppliedLookup(facility, previousPresetId);
		AddFacilityToAppliedLookup(facility);
	}

	private void ClearPresetFromAppliedFacilities(uint presetId)
	{
		if (facilitiesByPresetId.TryGetValue(presetId, out List<IFacility> facilities) == false)
			return;

		List<IFacility> affectedFacilities = new(facilities);
		for (int i = 0; i < affectedFacilities.Count; ++i)
		{
			IFacility facility = affectedFacilities[i];
			if (facility == null || facility.FacilityRulePresetId != presetId)
				continue;

			facility.SetFacilityRulePresetId(NoRulePresetId);
			OnFacilityRulePresetApplied?.Invoke(facility, presetId, NoRulePresetId);
		}

		facilitiesByPresetId.Remove(presetId);
	}

}
