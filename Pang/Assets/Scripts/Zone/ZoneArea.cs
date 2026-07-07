using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public class ZoneArea
{
	[SerializeField] private string displayName;
	[SerializeField] private ZoneType type;
	[SerializeField] private RectInt bound;
	[SerializeField] private int floor;
	[SerializeField] private uint runtimeBuildingId;
	private List<IFacility> occupiedFacilities = new();

	public string DisplayName => displayName;
	public ZoneType Type => type;
	public RectInt Bounds => bound;
	public int Floor => floor;
	public uint RuntimeBuildingId => runtimeBuildingId;
	public IReadOnlyList<IFacility> OccupiedFacilities => EnsureFacilityList();

	public ZoneArea(string name, ZoneType type, RectInt bound, int floor, uint runtimeBuildingId)
	{
		displayName = name;
		this.type = type;
		this.bound = bound;
		this.floor = floor;
		this.runtimeBuildingId = runtimeBuildingId;
	}

	public void Resize(in RectInt bound) => this.bound = bound;
	public void Rename(string newDisplayName) => displayName = newDisplayName;

	public bool RegisterFacility(IFacility facility)
	{
		List<IFacility> facilities = EnsureFacilityList();
		if (facility == null || facilities.Contains(facility))
			return false;

		facilities.Add(facility);
		return true;
	}

	public bool UnregisterFacility(IFacility facility)
	{
		List<IFacility> facilities = EnsureFacilityList();
		if (facility == null)
			return false;

		return facilities.Remove(facility);
	}

	public void ClearFacilities()
	{
		EnsureFacilityList().Clear();
	}

	public bool Contains(in int3 pos)
	{
		if (pos.y != floor)
			return false;

		return bound.Contains(new Vector2Int(pos.x, pos.z));
	}

	public void GetRandomPoint(out int3 val)
	{
		val = new int3(
			UnityEngine.Random.Range(bound.min.x, bound.max.x),
			floor,
			UnityEngine.Random.Range(bound.min.y, bound.max.y)
		);
	}

	private List<IFacility> EnsureFacilityList()
	{
		occupiedFacilities ??= new List<IFacility>();
		return occupiedFacilities;
	}

}
