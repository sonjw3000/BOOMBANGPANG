using System.Collections.Generic;

public sealed partial class BuildingManager
{
	public void ResetRuntimeState()
	{
		bool hadBuildings = registeredBuildings.Count > 0;
		registeredBuildings.Clear();
		buildingsById.Clear();
		nextRuntimeBuildingId = 1;
		if (hadBuildings)
			OnBuildingsChanged?.Invoke();
	}

	public BuildingManagerSaveData CaptureState()
	{
		BuildingManagerSaveData data = new();
		foreach (Building building in registeredBuildings)
		{
			if (building == null)
				continue;

			BuildingSaveData buildingData = new()
			{
				RuntimeBuildingId = building.RuntimeBuildingId,
				Name = building.DisplayName,
				State = building.State,
				WorkScope = building.WorkScope,
				OverrideCapsuleThreshold = building.OverrideCapsuleThreshold,
				CapsuleThresholdPercent = building.CapsuleThresholdPercent,
				SuitRemovalAllowed = building.SuitRemovalAllowed,
				TargetTemperatureCelsius = building.TargetTemperatureCelsius,
				OutputBuildingIds = new List<uint>(building.OutputBuildingIds),
			};

			for (int i = 0; i < building.InstalledAddons.Count; ++i)
			{
				var addon = building.InstalledAddons[i];
				if (addon == null || addon.Definition == null || string.IsNullOrWhiteSpace(addon.Definition.AddonId))
					continue;

				buildingData.Addons.Add(new BuildingAddonSaveData
				{
					DefinitionId = addon.Definition.AddonId,
					Health = addon.Health,
					Wear = addon.Wear,
				});
			}

			data.Buildings.Add(buildingData);
		}

		return data;
	}
}
