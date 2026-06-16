using System;
using System.Collections.Generic;
using UnityEngine;

public class CargoPort :
	ShelfBase
{
	// ib/ob 구분
	// 런타임에 수정되면 안된다
	[SerializeField] private bool isInbound = true;
	[SerializeField] private List<CargoPort> linkedPorts = new();

	private bool inputReady = true;

	public bool InputReady => inputReady;
	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.CargoPort;
	public bool IsInbound => isInbound;
	public IReadOnlyList<CargoPort> LinkedPorts => linkedPorts;

	public void SetInputReady(bool ready)
	{
		inputReady = ready;
	}

	public bool CanLinkTo(CargoPort target)
	{
		if (target == null || target == this)
			return false;

		if (IsInbound)
			return false;

		if (target.IsInbound == false)
			return false;

		return linkedPorts.Contains(target) == false;
	}

	public bool TryAddLinkedPort(CargoPort target)
	{
		if (CanLinkTo(target) == false)
			return false;

		linkedPorts.Add(target);
		return true;
	}

	public bool RemoveLinkedPort(CargoPort target)
	{
		if (target == null)
			return false;

		return linkedPorts.Remove(target);
	}

	public void RemoveInvalidLinks()
	{
		linkedPorts.RemoveAll(target => target == null || target == this || target.IsInbound == false);
	}

	public void ClearLinkedPorts()
	{
		linkedPorts.Clear();
	}

	public CargoPortSaveData CaptureState(Func<GameObject, int> getPlaceableId)
	{
		RemoveInvalidLinks();

		return new CargoPortSaveData
		{
			InputReady = inputReady,
			LinkedPortIds = CaptureLinkedPortIds(getPlaceableId),
		};
	}

	public void RestoreState(CargoPortSaveData data)
	{
		if (data == null)
			return;

		inputReady = data.InputReady;
	}

	public void RestoreLinks(CargoPortSaveData data, IReadOnlyDictionary<int, GameObject> restoredPlaceables)
	{
		linkedPorts.Clear();
		if (data?.LinkedPortIds == null || restoredPlaceables == null)
			return;

		for (int i = 0; i < data.LinkedPortIds.Count; ++i)
		{
			int linkedPortId = data.LinkedPortIds[i];
			if (restoredPlaceables.TryGetValue(linkedPortId, out GameObject linkedObject) == false ||
				linkedObject == null ||
				linkedObject.TryGetComponent(out CargoPort linkedPort) == false)
			{
				continue;
			}

			if (CanLinkTo(linkedPort))
				linkedPorts.Add(linkedPort);
		}
	}

	private List<int> CaptureLinkedPortIds(Func<GameObject, int> getPlaceableId)
	{
		List<int> linkedPortIds = new();
		if (getPlaceableId == null)
			return linkedPortIds;

		for (int i = 0; i < linkedPorts.Count; ++i)
		{
			CargoPort linkedPort = linkedPorts[i];
			if (linkedPort == null)
				continue;

			int linkedPortId = getPlaceableId(linkedPort.gameObject);
			if (linkedPortId <= 0 || linkedPortIds.Contains(linkedPortId))
				continue;

			linkedPortIds.Add(linkedPortId);
		}

		return linkedPortIds;
	}
}
