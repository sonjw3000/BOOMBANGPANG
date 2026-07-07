using System.Collections.Generic;
using UnityEngine;

public sealed partial class FacilityRuleManager
{
	public FacilityRuleManagerSaveData CaptureState()
	{
		FacilityRuleManagerSaveData data = new()
		{
			NextPresetId = nextPresetId,
		};

		for (int i = 0; i < presets.Count; ++i)
		{
			FacilityRulePreset preset = presets[i];
			if (preset == null || preset.Id == NoRulePresetId)
				continue;

			data.Presets.Add(new FacilityRulePresetSaveData
			{
				Id = preset.Id,
				DisplayName = preset.DisplayName,
				Color = ToSave(preset.Color),
				Rule = CaptureRule(preset.Rule),
			});
		}

		return data;
	}

	public void RestoreState(FacilityRuleManagerSaveData data)
	{
		presets.Clear();
		presetsById.Clear();
		facilitiesByPresetId.Clear();
		nextPresetId = data != null && data.NextPresetId != NoRulePresetId ? data.NextPresetId : 1;

		if (data?.Presets != null)
		{
			for (int i = 0; i < data.Presets.Count; ++i)
			{
				FacilityRulePresetSaveData presetData = data.Presets[i];
				if (presetData == null || presetData.Id == NoRulePresetId)
					continue;

				FacilityRulePreset preset = new(
					presetData.Id,
					string.IsNullOrWhiteSpace(presetData.DisplayName) ? $"Rule {presetData.Id}" : presetData.DisplayName,
					FromSave(presetData.Color),
					RestoreRule(presetData.Rule));
				presets.Add(preset);
			}
		}

		RebuildPresetLookup();
		RebuildAppliedFacilityLookup();
	}

	public void ResetRuntimeState()
	{
		presets.Clear();
		presetsById.Clear();
		facilitiesByPresetId.Clear();
		nextPresetId = 1;
		OnPresetsRebuilt?.Invoke();
	}

	private static FacilityRuleSaveData CaptureRule(FacilityRule rule)
	{
		FacilityRuleSaveData data = new();
		if (rule == null)
			return data;

		data.Priority = rule.Priority;
		data.ItemRule = CaptureItemRule(rule.ItemRule);
		data.WorkerRule = CaptureWorkerRule(rule.WorkerRule);
		return data;
	}

	private static FacilityItemRuleSaveData CaptureItemRule(FacilityItemRule rule)
	{
		FacilityItemRuleSaveData data = new();
		if (rule == null)
			return data;

		data.RequiredItemTags = rule.RequiredItemTags;
		data.ForbiddenItemTags = rule.ForbiddenItemTags;
		data.RequiredItemStatus = rule.RequiredItemStatus;
		CaptureItemIds(rule.WhiteList, data.WhiteListItemIds);
		CaptureItemIds(rule.BlackList, data.BlackListItemIds);
		return data;
	}

	private static FacilityWorkerRuleSaveData CaptureWorkerRule(FacilityWorkerRule rule)
	{
		FacilityWorkerRuleSaveData data = new();
		if (rule == null)
			return data;

		data.RequiredWorkerKind = rule.RequiredWorkerKind;
		data.RequiredWorkerAbility = rule.RequiredWorkerAbility;
		data.RequiredHumanTypes.AddRange(rule.RequiredHumanTypes);
		data.ForbiddenHumanTypes.AddRange(rule.ForbiddenHumanTypes);
		data.RequiredRobotTypes.AddRange(rule.RequiredRobotTypes);
		data.ForbiddenRobotTypes.AddRange(rule.ForbiddenRobotTypes);
		return data;
	}

	private static void CaptureItemIds(IReadOnlyList<ItemDefinition> items, List<uint> targetIds)
	{
		targetIds.Clear();
		if (items == null)
			return;

		for (int i = 0; i < items.Count; ++i)
		{
			ItemDefinition item = items[i];
			if (item != null)
				targetIds.Add(item.ItemID);
		}
	}

	private FacilityRule RestoreRule(FacilityRuleSaveData data)
	{
		FacilityRule rule = new();
		if (data == null)
			return rule;

		rule.SetPriority(data.Priority);
		rule.SetItemRule(RestoreItemRule(data.ItemRule));
		rule.SetWorkerRule(RestoreWorkerRule(data.WorkerRule));
		return rule;
	}

	private FacilityItemRule RestoreItemRule(FacilityItemRuleSaveData data)
	{
		FacilityItemRule rule = new();
		if (data == null)
			return rule;

		rule.SetRequiredItemTags(data.RequiredItemTags);
		rule.SetForbiddenItemTags(data.ForbiddenItemTags);
		rule.SetRequiredItemStatus(data.RequiredItemStatus);
		rule.SetWhiteList(RestoreItemDefinitions(data.WhiteListItemIds));
		rule.SetBlackList(RestoreItemDefinitions(data.BlackListItemIds));
		return rule;
	}

	private static FacilityWorkerRule RestoreWorkerRule(FacilityWorkerRuleSaveData data)
	{
		FacilityWorkerRule rule = new();
		if (data == null)
			return rule;

		rule.SetRequiredWorkerKind(data.RequiredWorkerKind);
		rule.SetRequiredWorkerAbility(data.RequiredWorkerAbility);
		rule.SetRequiredHumanTypes(data.RequiredHumanTypes);
		rule.SetForbiddenHumanTypes(data.ForbiddenHumanTypes);
		rule.SetRequiredRobotTypes(data.RequiredRobotTypes);
		rule.SetForbiddenRobotTypes(data.ForbiddenRobotTypes);
		return rule;
	}

	private IEnumerable<ItemDefinition> RestoreItemDefinitions(IReadOnlyList<uint> itemIds)
	{
		if (itemIds == null || itemIds.Count == 0 || GameContext.HasInstance == false || GameContext.Instance.ItemDB == null)
			yield break;

		for (int i = 0; i < itemIds.Count; ++i)
		{
			uint itemId = itemIds[i];
			if (GameContext.Instance.ItemDB.GetItemData(itemId, out ItemDefinition itemDefinition) && itemDefinition != null)
			{
				yield return itemDefinition;
				continue;
			}

			Debug.LogWarning($"[Save] Missing item definition {itemId} while restoring facility rule.");
		}
	}

	private static ColorSaveData ToSave(Color color) => new(color.r, color.g, color.b, color.a);
	private static Color FromSave(ColorSaveData color) => new(color.R, color.G, color.B, color.A);
}
