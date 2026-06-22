using System;
using System.Collections.Generic;
using UnityEngine;

public abstract partial class CargoPort : CapsuleDock
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
