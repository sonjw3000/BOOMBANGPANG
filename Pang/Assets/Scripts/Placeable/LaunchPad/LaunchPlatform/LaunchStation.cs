using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

// 런치 패드 베이스


public class LaunchStation
	: MonoBehaviour
	, IGridPlaceable
	, IGridPlacementEffect
	, IInteractionPoint
{
	[SerializeField] private List<PlatformAddon> addons = new();

	private int3 gridPosition;
	private List<int3> interactionPoints = new();

	public int3 GridPosition => gridPosition;
	public IReadOnlyList<int3> InteractionPoints => interactionPoints;


	private void Awake()
	{
		foreach (var addon in addons)
		{
			addon.SetPadBase(this);
		}
	}

	public bool TryGetStoreablePad(out CargoStorageAddon addon)
	{
		foreach (var a in addons)
		{
			if (a.GetComponent<CargoStorageAddon>() == null)
				continue;
			addon = (CargoStorageAddon)a;
			return true;
		}

		addon = null;
		return false;
	}

	public bool TryGetLoadablePad(in BoxBase cargo, out LaunchPadAddon addon)
	{
		foreach (var a in addons)
		{
			if (a.GetComponent<CargoStorageAddon>() == null)
				continue;

			addon = (LaunchPadAddon)a;
			return true;
		}

		addon = null;
		return false;
	}


	public void OnPositionSet(in int3 position)
	{
		// 
		gridPosition = position;
	}

	public void OnRemoved()
	{
	}

	public void OnDestroyedBy(in DestroyContext ctx)
	{
	}



}