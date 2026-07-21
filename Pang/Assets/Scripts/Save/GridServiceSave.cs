using Assets.Scripts.Save.JsonData;
using UnityEngine;

public partial class GridService
{
	public GridMapSaveData CaptureState()
	{
		GridMapSaveData data = new();
		data.MapSize = new Int3SaveData(MapSize.x, MapSize.y, MapSize.z);
		data.Tiles = new int[MapSize.x * MapSize.y * MapSize.z];
		data.Temperatures = new float[data.Tiles.Length];
		data.OxygenLevels = new float[data.Tiles.Length];
		data.FireIntensities = new float[data.Tiles.Length];
		data.ContaminationLevels = new float[data.Tiles.Length];
		data.CorrosiveLevels = new float[data.Tiles.Length];
		data.RadiationLevels = new float[data.Tiles.Length];

		for (int x = 0; x < MapSize.x; ++x)
		{
			for (int y = 0; y < MapSize.y; ++y)
			{
				for (int z = 0; z < MapSize.z; ++z)
				{
					int idx = x + MapSize.x * (y + MapSize.y * z);
					GridCell cell = Map[x, y, z];
					data.Tiles[idx] = cell != null ? cell.Tile : 0;
					data.Temperatures[idx] = cell != null ? cell.TemperatureCelsius : GridCell.DefaultTemperatureCelsius;
					data.OxygenLevels[idx] = cell != null ? cell.Oxygen : GridCell.DefaultOxygen;
					data.FireIntensities[idx] = cell != null ? cell.FireIntensity : GridCell.MinimumHazardLevel;
					data.ContaminationLevels[idx] = cell != null ? cell.ContaminationLevel : GridCell.MinimumHazardLevel;
					data.CorrosiveLevels[idx] = cell != null ? cell.CorrosiveLevel : GridCell.MinimumHazardLevel;
					data.RadiationLevels[idx] = cell != null ? cell.RadiationLevel : GridCell.MinimumHazardLevel;
				}
			}
		}

		return data;
	}

	public void RestoreState(GridMapSaveData data)
	{
		if (data == null)
		{
			BuildDefaultMap();
			OnGameStart();
			return;
		}

		GridMapData gridData = new()
		{
			X = data.MapSize.X,
			Y = data.MapSize.Y,
			Z = data.MapSize.Z,
			Tiles = data.Tiles,
			Temperatures = data.Temperatures,
			OxygenLevels = data.OxygenLevels,
			FireIntensities = data.FireIntensities,
			ContaminationLevels = data.ContaminationLevels,
			CorrosiveLevels = data.CorrosiveLevels,
			RadiationLevels = data.RadiationLevels,
		};

		gridMap.LoadByData(gridData);
		OnGameStart();
	}

	public void ResetRuntimeState()
	{
		foreach (Transform child in placeableParent.transform)
		{
			child.gameObject.SetActive(false);
			Destroy(child.gameObject);
		}

		foreach (Transform child in gridParent.transform)
			Destroy(child.gameObject);

		if (gridBoundaryTexture != null)
		{
			Destroy(gridBoundaryTexture);
			gridBoundaryTexture = null;
		}

		gridBoundaryQuad = null;
		placedObjects.Clear();
		IsReady = false;
	}
}
