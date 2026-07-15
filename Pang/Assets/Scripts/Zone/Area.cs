using System;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public sealed class Area
{
	[SerializeField] private string displayName;
	[SerializeField] private AreaType type;
	[SerializeField] private RectInt bounds;
	[SerializeField] private int floor;
	[SerializeField] private uint destinationBuildingId;

	public string DisplayName => displayName;
	public AreaType Type => type;
	public RectInt Bounds => bounds;
	public int Floor => floor;
	public uint DestinationBuildingId => destinationBuildingId;

	public Area(string name, AreaType type, in RectInt bounds, int floor)
	{
		displayName = name;
		this.type = type;
		this.bounds = bounds;
		this.floor = floor;
	}

	public void Resize(in RectInt newBounds) => bounds = newBounds;
	public void Rename(string newDisplayName) => displayName = newDisplayName;
	internal void SetDestinationBuildingId(uint buildingId) => destinationBuildingId = buildingId;

	public bool Contains(in int3 position)
	{
		return position.y == floor && bounds.Contains(new Vector2Int(position.x, position.z));
	}

	public void GetRandomPoint(out int3 value)
	{
		value = new int3(
			UnityEngine.Random.Range(bounds.xMin, bounds.xMax),
			floor,
			UnityEngine.Random.Range(bounds.yMin, bounds.yMax));
	}
}
