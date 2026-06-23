using System;
using UnityEngine;

public abstract partial class CargoPort
{
	public CargoPortSaveData CaptureState(Func<GameObject, int> getPlaceableId)
	{
		return new CargoPortSaveData
		{
			InputReady = HasPayload == false,
		};
	}

	public void RestoreState(CargoPortSaveData data)
	{
		if (data == null)
			return;

		// Current CargoPort runtime state is driven by the docked capsule itself.
		// Keep the method for save compatibility even though InputReady is no longer stored here.
	}
}
