using System.Collections.Generic;
using UnityEngine;

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
		for (var it = cargosToLaunch.First; it != null; )
		{
			var next = it.Next;

			if (station.TryGetLaunchablePad(it.Value, out var pad))
			{
				pad.TryLoad(it.Value);
				cargosToLaunch.Remove(it);
			}

			it = next;
		}
	}

}
