using System.Collections.Generic;

public sealed partial class BuildingManager
{
	public void ResetRuntimeState()
	{
		registeredBuildings.Clear();
		buildingsById.Clear();
		nextRuntimeBuildingId = 1;
	}

	public BuildingManagerSaveData CaptureState()
	{
		BuildingManagerSaveData data = new();
		foreach (Building building in registeredBuildings)
		{
			if (building == null)
				continue;

			data.Buildings.Add(new BuildingSaveData
			{
				RuntimeBuildingId = building.RuntimeBuildingId,
				Name = building.DisplayName,
				Type = building.Type,
				State = building.State,
				WorkScope = building.WorkScope,
				OverrideCapsuleThreshold = building.OverrideCapsuleThreshold,
				CapsuleThresholdPercent = building.CapsuleThresholdPercent,
				OutputBuildingIds = new List<uint>(building.OutputBuildingIds),
			});
		}

		return data;
	}
}
