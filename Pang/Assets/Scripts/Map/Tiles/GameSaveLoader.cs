using UnityEngine;
using System.Collections.Generic;

namespace JsonData
{

	[System.Serializable]
	public class Placeable
	{
		public int x, y, z;
		public string placeableID;
		public FacingDirection facingDirection;
	}

	[System.Serializable]
	public class PlaceableData
	{
		public List<Placeable> placeables;
	}

	[System.Serializable]
	public class GridMapData
	{
		public int X, Y, Z;
		public int[] Tiles;
	}

	[System.Serializable]
	public class BasicData
	{
		public int SaveCount;
		public string Version;
	}



	[System.Serializable]
	public class SaveData
	{
		public BasicData BasicData;
		public GridMapData MapData;
		public PlaceableData PlaceableData;

	}

};


public class GameSaveLoader
{
	private JsonData.SaveData data;

	public bool LoadMap(string mapPath)
	{
		data = JsonUtility.FromJson<JsonData.SaveData>(mapPath);

		return true;
	}

	public JsonData.GridMapData GetGrid() => data.MapData;
	public JsonData.PlaceableData GetPlaceable() => data.PlaceableData;

}
