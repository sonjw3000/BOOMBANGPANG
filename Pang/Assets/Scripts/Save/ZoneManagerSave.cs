using System.Collections.Generic;
using UnityEngine;

public partial class ZoneManager
{
	public ZoneManagerSaveData CaptureState()
	{
		ZoneManagerSaveData data = new();
		foreach (var zone in registeredZones)
		{
			if (zone == null)
				continue;

			data.Zones.Add(new ZoneSaveData
			{
				Name = zone.DisplayName,
				Type = zone.Type,
				RuntimeBuildingId = zone.RuntimeBuildingId,
				Floor = zone.Floor,
				Bounds = new RectIntSaveData(zone.Bounds.x, zone.Bounds.y, zone.Bounds.width, zone.Bounds.height),
				Rule = CaptureRule(zone.Rule),
			});
		}

		return data;
	}

	public void RestoreState(ZoneManagerSaveData data)
	{
		ResetRuntimeState();
		if (data == null)
			return;

		foreach (var zoneData in data.Zones)
		{
			RectInt bounds = new(zoneData.Bounds.X, zoneData.Bounds.Y, zoneData.Bounds.Width, zoneData.Bounds.Height);
			if (zoneData.RuntimeBuildingId == 0)
			{
				ZoneArea restoredGlobalZone = AddGlobalZone(zoneData.Name, zoneData.Type, bounds, zoneData.Floor);
				if (restoredGlobalZone == null)
					Debug.LogWarning($"[Save] Failed to restore global zone {zoneData.Name}.");
				else
					SetZoneRule(restoredGlobalZone, RestoreRule(zoneData.Rule));

				continue;
			}

			if (BuildingManager == null || BuildingManager.TryGetBuilding(zoneData.RuntimeBuildingId, out Building ownerBuilding) == false)
			{
				Debug.LogWarning($"[Save] Skipping zone restore {zoneData.Name}: missing building {zoneData.RuntimeBuildingId}.");
				continue;
			}

			ZoneArea restoredZone = AddZone(ownerBuilding, zoneData.Name, zoneData.Type, bounds, zoneData.Floor);
			if (restoredZone == null)
				Debug.LogWarning($"[Save] Failed to restore zone {zoneData.Name} for building {zoneData.RuntimeBuildingId}.");
			else
				SetZoneRule(restoredZone, RestoreRule(zoneData.Rule));
		}
	}

	public void ResetRuntimeState()
	{
		registeredZones.Clear();
		RebuildZoneLookup();
	}

	private static ZoneRuleSaveData CaptureRule(ZoneRule rule)
	{
		ZoneRuleSaveData data = new();
		if (rule == null)
			return data;

		data.Priority = rule.Priority;
		data.ItemRule = CaptureItemRule(rule.ItemRule);
		data.WorkerRule = CaptureWorkerRule(rule.WorkerRule);
		return data;
	}

	private static ZoneItemRuleSaveData CaptureItemRule(ZoneItemRule rule)
	{
		ZoneItemRuleSaveData data = new();
		if (rule == null)
			return data;

		data.RequiredItemTags = rule.RequiredItemTags;
		data.ForbiddenItemTags = rule.ForbiddenItemTags;
		CaptureItemIds(rule.WhiteList, data.WhiteListItemIds);
		CaptureItemIds(rule.BlackList, data.BlackListItemIds);
		return data;
	}

	private static ZoneWorkerRuleSaveData CaptureWorkerRule(ZoneWorkerRule rule)
	{
		ZoneWorkerRuleSaveData data = new();
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

	private ZoneRule RestoreRule(ZoneRuleSaveData data)
	{
		ZoneRule rule = new();
		if (data == null)
			return rule;

		rule.SetPriority(data.Priority);
		rule.SetItemRule(RestoreItemRule(data.ItemRule));
		rule.SetWorkerRule(RestoreWorkerRule(data.WorkerRule));
		return rule;
	}

	private ZoneItemRule RestoreItemRule(ZoneItemRuleSaveData data)
	{
		ZoneItemRule rule = new();
		if (data == null)
			return rule;

		rule.SetRequiredItemTags(data.RequiredItemTags);
		rule.SetForbiddenItemTags(data.ForbiddenItemTags);
		rule.SetWhiteList(RestoreItemDefinitions(data.WhiteListItemIds));
		rule.SetBlackList(RestoreItemDefinitions(data.BlackListItemIds));
		return rule;
	}

	private static ZoneWorkerRule RestoreWorkerRule(ZoneWorkerRuleSaveData data)
	{
		ZoneWorkerRule rule = new();
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

			Debug.LogWarning($"[Save] Missing item definition {itemId} while restoring zone rule.");
		}
	}
}
