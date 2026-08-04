using System;
using UnityEngine;

public abstract partial class CargoPort
{
	public CargoPortSaveData CaptureState(Func<GameObject, int> getPlaceableId)
	{
		CargoPortSaveData data = new CargoPortSaveData
		{
			InputReady = HasPayload == false,
		};
		if (DockedCapsule != null)
		{
			data.Box = new BoxReferenceSaveData
			{
				BoxType = DockedCapsule.Type,
				BoxId = DockedCapsule.BoxId,
			};
		}

		return data;
	}

	public void RestoreState(CargoPortSaveData data)
	{
		if (data == null)
			return;

		if (data.Box == null || HasCapsule)
			return;

		if (GameContext.Instance.BoxMgr.TryGetBox(data.Box.BoxType, data.Box.BoxId, out BoxBase box))
			PutBox(box);
	}
}
