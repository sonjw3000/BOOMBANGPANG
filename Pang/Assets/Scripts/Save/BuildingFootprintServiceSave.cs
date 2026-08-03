using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed partial class BuildingFootprintService
{
	public void ResetRuntimeState()
	{
		registeredFootprints.Clear();
	}

	public BuildingFootprintServiceSaveData CaptureState()
	{
		BuildingFootprintServiceSaveData data = new();
		foreach (BuildingFootprintRecord footprint in registeredFootprints)
		{
			if (footprint == null)
				continue;

			data.Footprints.Add(new BuildingFootprintSaveData
			{
				RuntimeBuildingId = footprint.RuntimeBuildingId,
				PresetId = footprint.PresetId,
				Floor = footprint.Floor,
				CenterX = footprint.Center.x,
				CenterZ = footprint.Center.y,
				Bounds = new RectIntSaveData(footprint.Bounds.x, footprint.Bounds.y, footprint.Bounds.width, footprint.Bounds.height),
			});
		}

		return data;
	}

	public void RestoreState(BuildingManagerSaveData buildingData, BuildingFootprintServiceSaveData footprintData)
	{
		registeredFootprints.Clear();
		if (footprintData == null)
			return;

		Dictionary<uint, BuildingSaveData> buildingsById = new();
		if (buildingData != null)
		{
			foreach (BuildingSaveData savedBuilding in buildingData.Buildings)
			{
				if (savedBuilding == null || savedBuilding.RuntimeBuildingId == 0)
					continue;

				buildingsById[savedBuilding.RuntimeBuildingId] = savedBuilding;
			}
		}

		foreach (BuildingFootprintSaveData savedFootprint in footprintData.Footprints)
		{
			if (savedFootprint == null || savedFootprint.RuntimeBuildingId == 0)
				continue;

			RectInt bounds;
			Vector2Int center;
			List<GridCell> ownedCells;
			int addonSlotCapacity = 0;
			if (string.IsNullOrWhiteSpace(savedFootprint.PresetId))
			{
				bounds = new RectInt(savedFootprint.Bounds.X, savedFootprint.Bounds.Y, savedFootprint.Bounds.Width, savedFootprint.Bounds.Height);
				center = new Vector2Int(
					bounds.xMin + ((bounds.width - 1) / 2),
					bounds.yMin + ((bounds.height - 1) / 2));
				ownedCells = BuildOwnedCells(bounds, savedFootprint.Floor);
				if (ownedCells.Count != bounds.width * bounds.height)
				{
					Debug.LogWarning($"[Save] Failed to rebuild legacy building footprint {savedFootprint.RuntimeBuildingId}: owned cell count mismatch.");
					continue;
				}
			}
			else
			{
				if (TryGetPreset(savedFootprint.PresetId, out BuildingFootprintPreset preset) == false)
				{
					Debug.LogWarning($"[Save] Missing building footprint preset {savedFootprint.PresetId} for building {savedFootprint.RuntimeBuildingId}.");
					continue;
				}

				center = new Vector2Int(savedFootprint.CenterX, savedFootprint.CenterZ);
				bounds = preset.GetBounds(center);
				addonSlotCapacity = preset.AddonSlotCapacity;
				int3 centerPosition = new(center.x, savedFootprint.Floor, center.y);
				ownedCells = BuildOwnedCells(centerPosition, preset, savedFootprint.Floor);
			}

			buildingsById.TryGetValue(savedFootprint.RuntimeBuildingId, out BuildingSaveData savedBuilding);

			Building restoredBuilding = BuildingManager.RestoreBuilding(
				ownedCells,
				savedFootprint.RuntimeBuildingId,
				savedBuilding != null ? savedBuilding.Type : BuildingType.Generic,
				string.IsNullOrWhiteSpace(savedBuilding?.Name) ? $"Building {savedFootprint.RuntimeBuildingId}" : savedBuilding.Name,
				savedBuilding != null ? savedBuilding.State : BuildingState.Active,
				savedBuilding != null ? savedBuilding.WorkScope : BuildingWorkScope.HomeOnly,
				savedBuilding != null && savedBuilding.OverrideCapsuleThreshold,
				savedBuilding != null ? savedBuilding.CapsuleThresholdPercent : 80.0f,
				savedBuilding != null && savedBuilding.SuitRemovalAllowed,
				addonSlotCapacity);

			if (restoredBuilding == null)
			{
				Debug.LogWarning($"[Save] Failed to restore building runtime data {savedFootprint.RuntimeBuildingId}.");
				continue;
			}

			registeredFootprints.Add(new BuildingFootprintRecord
			{
				RuntimeBuildingId = restoredBuilding.RuntimeBuildingId,
				PresetId = savedFootprint.PresetId,
				Floor = savedFootprint.Floor,
				Center = center,
				Bounds = bounds,
			});
		}
	}
}
