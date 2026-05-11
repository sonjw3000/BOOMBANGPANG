using System;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public class ZoneArea
{
	[SerializeField] private string displayName;
	[SerializeField] private ZoneType type;
	[SerializeField] private RectInt bound;
	[SerializeField] private int floor;

	public string DisplayName => displayName;
	public ZoneType Type => type;
	public RectInt Bounds => bound;
	public int Floor => floor;

	public ZoneArea(string name, ZoneType type, RectInt bound, int floor)
	{
		displayName = name;
		this.type = type;
		this.bound = bound;
		this.floor = floor;
	}

	public void Resize(in RectInt bound) => this.bound = bound;

	public void GetRandomPoint(out int3 val)
	{
		val = new int3(
			UnityEngine.Random.Range(bound.min.x, bound.max.x),
			floor,
			UnityEngine.Random.Range(bound.min.y, bound.max.y)
		);
	}
}
