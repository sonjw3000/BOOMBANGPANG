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
		}
	}

	public void ResetRuntimeState()
	{
		registeredZones.Clear();
		RebuildZoneLookup();
	}

}
