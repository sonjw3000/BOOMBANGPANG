using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class CargoPort : CapsuleDock
{
	[SerializeField] private List<CargoPort> linkedPorts = new();

	public event Action<CargoPort> OnCargoDocked;
	public event Action<CargoPort> OnCargoUndocked;
	public event Action<CargoPort> OnCargoQuantityZero;
	public event Action<CargoPort> OnCargoQuantityOverPercent;

	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.CargoPort;
	public bool HasPayload => DockedCapsule != null;
	public IReadOnlyList<CargoPort> LinkedPorts => linkedPorts;
	
	protected CargoPortService CargoPortSvc => GameContext.Instance.CargoPortSvc;

	public abstract string PortRoleLabel { get; }

	// link
	public abstract bool CanLinkTo(CargoPort target);
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
		linkedPorts.RemoveAll(target => target == null || target == this);
	}

	public void ClearLinkedPorts()
	{
		linkedPorts.Clear();
	}

	// save load
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



	// cargo handling
	protected override void OnDockedCapsuleChanged()
	{
		if (HasCapsule)
			OnCargoDocked?.Invoke(this);
		else
			OnCargoUndocked?.Invoke(this);
	}

	protected override void OnCapsuleQuantityChanged()
	{
		if (DockedCapsule == null)
			return;

		if (FilledPercent <= 0)
			OnCargoQuantityZero?.Invoke(this);
		else if (FilledPercent >= CargoPortSvc.CargoStandardPercent)
			OnCargoQuantityOverPercent?.Invoke(this);
	}
}
