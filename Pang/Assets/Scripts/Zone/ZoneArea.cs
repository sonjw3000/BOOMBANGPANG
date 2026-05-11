using Unity.Mathematics;
using UnityEngine;

public class ZoneArea
{
	private string displayName;
	private ZoneType type;
	private RectInt bound;
	private int floor;

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
		var rand = new System.Random();
		val = new int3(
			rand.Next(bound.min.x, bound.max.x),
			floor,
			rand.Next(bound.min.y, bound.max.y)
		);
	}

}
