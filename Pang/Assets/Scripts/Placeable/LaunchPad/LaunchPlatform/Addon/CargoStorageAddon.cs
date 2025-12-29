using UnityEngine;
using System.Collections.Generic;

public class CargoStorageAddon : PlatformAddon
{
	[SerializeField] private int maxCargoSlot = 10;

	// queue 식으로 사용한다
	private LinkedList<BoxBase> cargosToLaunch = new();


	public void StoreCargo(BoxBase cargo)
	{
		cargosToLaunch.AddLast(cargo);
	}


	private void Update()
	{
		foreach (var cargo in cargosToLaunch)
		{
			if (station.TryGetLoadablePad(cargo, out var pad))
			{
				pad.TryLoad(cargo);
				cargosToLaunch.Remove(cargo);
			}
		}
	}

}
