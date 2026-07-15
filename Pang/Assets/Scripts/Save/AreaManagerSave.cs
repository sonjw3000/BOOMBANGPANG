using UnityEngine;

public partial class AreaManager
{
	public AreaManagerSaveData CaptureState()
	{
		AreaManagerSaveData data = new();
		for (int i = 0; i < registeredAreas.Count; ++i)
		{
			Area area = registeredAreas[i];
			if (area == null)
				continue;

			data.Areas.Add(new AreaSaveData
			{
				Name = area.DisplayName,
				Type = area.Type,
				Floor = area.Floor,
				DestinationBuildingId = area.DestinationBuildingId,
				Bounds = new RectIntSaveData(area.Bounds.x, area.Bounds.y, area.Bounds.width, area.Bounds.height),
			});
		}

		return data;
	}

	public void RestoreState(AreaManagerSaveData data)
	{
		ResetRuntimeState();
		if (data == null)
			return;

		for (int i = 0; i < data.Areas.Count; ++i)
		{
			AreaSaveData areaData = data.Areas[i];
			if (areaData == null)
				continue;

			RectInt bounds = new(
				areaData.Bounds.X,
				areaData.Bounds.Y,
				areaData.Bounds.Width,
				areaData.Bounds.Height);
			Area area = AddArea(areaData.Name, areaData.Type, bounds, areaData.Floor);
			if (area == null)
				Debug.LogWarning($"[Save] Failed to restore area {areaData.Name}.");
			else if (areaData.DestinationBuildingId != 0 && TrySetDestinationBuilding(area, areaData.DestinationBuildingId) == false)
				Debug.LogWarning($"[Save] Failed to restore destination for area {areaData.Name}.");
		}
	}

	public void ResetRuntimeState()
	{
		registeredAreas.Clear();
		RebuildAreaLookup();
	}
}
