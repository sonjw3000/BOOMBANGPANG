using System;
using UnityEngine;

public partial class UnloadingTask
{
	public UnloadingTaskSaveData CaptureState(Func<GameObject, int> getPlaceableId)
	{
		return new UnloadingTaskSaveData
		{
			TargetRocketId = targetRocket != null && getPlaceableId != null ? getPlaceableId(targetRocket.gameObject) : -1,
			CargoPortId = cargoPort != null && getPlaceableId != null ? getPlaceableId(cargoPort.gameObject) : -1,
			IsUnloadEnd = IsUnloadEnd,
		};
	}

	public void RestoreState(CargoPort cargoPort, bool isUnloadEnd)
	{
		this.cargoPort = cargoPort;
		IsUnloadEnd = isUnloadEnd;
	}
}
