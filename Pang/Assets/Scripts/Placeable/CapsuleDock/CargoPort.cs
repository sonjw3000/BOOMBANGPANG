using System;
using UnityEngine;

public abstract partial class CargoPort : CapsuleDock
{
	public event Action<CargoPort, CargoCapsule> OnCargoUndocking;
	public event Action<CargoPort> OnCargoQuantityZero;
	public event Action<CargoPort> OnCargoQuantityOverPercent;

	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.CargoPort;
	public bool HasPayload => DockedCapsule != null;
	
	protected CargoPortService CargoPortSvc => GameContext.Instance.CargoPortSvc;

	public abstract string PortRoleLabel { get; }

	// cargo handling
	protected override void OnBeforeCapsuleUndocked(CargoCapsule capsule)
	{
		OnCargoUndocking?.Invoke(this, capsule);
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
