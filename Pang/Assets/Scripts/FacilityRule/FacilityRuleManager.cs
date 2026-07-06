using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class FacilityRuleManager : MonoBehaviour
{
	public const uint NoRulePresetId = 0;

	[SerializeField] private uint nextPresetId = 1;
	[SerializeField] private List<FacilityRulePreset> presets = new();

	private readonly Dictionary<uint, FacilityRulePreset> presetsById = new();

	public IReadOnlyList<FacilityRulePreset> Presets => presets;

	public event Action<FacilityRulePreset> OnPresetCreated;
	public event Action<FacilityRulePreset> OnPresetChanged;
	public event Action<uint> OnPresetDeleted;
	public event Action<IFacility, uint, uint> OnFacilityRulePresetApplied;
	public event Action OnPresetsRebuilt;

	private void Awake()
	{
		RebuildPresetLookup();
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

		for (int i = presets.Count - 1; i >= 0; --i)
		{
			if (presets[i] != null && presets[i].Id == presetId)
				presets.RemoveAt(i);
		}

		OnPresetDeleted?.Invoke(presetId);
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
			return true;

		facility.SetFacilityRulePresetId(presetId);
		OnFacilityRulePresetApplied?.Invoke(facility, previousPresetId, presetId);
		return true;
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
}
