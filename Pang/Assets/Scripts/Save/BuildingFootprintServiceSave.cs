using System.Collections.Generic;
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
				Floor = footprint.Floor,
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

			RectInt bounds = new(savedFootprint.Bounds.X, savedFootprint.Bounds.Y, savedFootprint.Bounds.Width, savedFootprint.Bounds.Height);
			List<GridCell> ownedCells = BuildOwnedCells(bounds, savedFootprint.Floor);
			if (ownedCells.Count != bounds.width * bounds.height)
			{
				Debug.LogWarning($"[Save] Failed to rebuild building footprint {savedFootprint.RuntimeBuildingId}: owned cell count mismatch.");
				continue;
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
				savedBuilding != null ? savedBuilding.CapsuleThresholdPercent : 80.0f);

			if (restoredBuilding == null)
			{
				Debug.LogWarning($"[Save] Failed to restore building runtime data {savedFootprint.RuntimeBuildingId}.");
				continue;
			}

			registeredFootprints.Add(new BuildingFootprintRecord
			{
				RuntimeBuildingId = restoredBuilding.RuntimeBuildingId,
				Floor = savedFootprint.Floor,
				Bounds = bounds,
			});
		}
	}
}
