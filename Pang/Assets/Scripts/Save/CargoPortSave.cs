using System;
using System.Collections.Generic;
using UnityEngine;

public abstract partial class CargoPort
{
	public CargoPortSaveData CaptureState(Func<GameObject, int> getPlaceableId)
	{
		RemoveInvalidLinks();

		return new CargoPortSaveData
		{
			InputReady = HasPayload == false,
			LinkedPortIds = CaptureLinkedPortIds(getPlaceableId),
		};
	}

	public void RestoreState(CargoPortSaveData data)
	{
		if (data == null)
			return;

		// Current CargoPort runtime state is driven by the docked capsule itself.
		// Keep the method for save compatibility even though InputReady is no longer stored here.
	}

	public void RestoreLinks(CargoPortSaveData data, IReadOnlyDictionary<int, GameObject> restoredPlaceables)
	{
		linkedPorts.Clear();
		if (data?.LinkedPortIds == null || restoredPlaceables == null)
			return;

		HashSet<CargoPort> restoredLinks = new();
		for (int i = 0; i < data.LinkedPortIds.Count; ++i)
		{
			int linkedPortId = data.LinkedPortIds[i];
			if (restoredPlaceables.TryGetValue(linkedPortId, out GameObject linkedObject) == false ||
				linkedObject == null ||
				linkedObject.TryGetComponent(out CargoPort linkedPort) == false ||
				linkedPort == null ||
				linkedPort == this)
			{
				continue;
			}

			if (restoredLinks.Add(linkedPort))
				linkedPorts.Add(linkedPort);
		}
	}

	private List<int> CaptureLinkedPortIds(Func<GameObject, int> getPlaceableId)
	{
		List<int> linkedPortIds = new();
		if (getPlaceableId == null)
			return linkedPortIds;

		HashSet<int> capturedIds = new();
		for (int i = 0; i < linkedPorts.Count; ++i)
		{
			CargoPort linkedPort = linkedPorts[i];
			if (linkedPort == null)
				continue;

			int linkedPortId = getPlaceableId(linkedPort.gameObject);
			if (linkedPortId <= 0 || capturedIds.Add(linkedPortId) == false)
				continue;

			linkedPortIds.Add(linkedPortId);
		}

		return linkedPortIds;
	}
}
