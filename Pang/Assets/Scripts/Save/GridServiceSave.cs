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
