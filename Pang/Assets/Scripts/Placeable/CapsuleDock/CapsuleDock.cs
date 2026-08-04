using UnityEngine;
using System;

public enum CapsuleDockState
{
	IB = 0,
	OBStandby = 1,
	Empty = 2,
	IBStandby = 3,
	OB = 4,
	InboundSource = 5,
	WasteBin = 6,
	WasteOutbound = 7,
	WasteContainer = 8,
}

public abstract class CapsuleDock : BoxInteraction, IFacilityUserRemovalGuard
{
	protected CargoCapsule dockedCapsule = null;
	[SerializeField] private CargoRouteKind acceptedCargoRouteKind = CargoRouteKind.Standard;

	public event Action<CapsuleDock> OnCapsuleDocked;
	public event Action<CapsuleDock> OnCapsuleUndocked;

	public CargoCapsule DockedCapsule => dockedCapsule;
	public virtual CapsuleDockState DockState => CapsuleDockState.Empty;
	public bool HasCapsule => dockedCapsule != null;
	protected virtual CargoRouteKind SupportedCargoRouteKind => acceptedCargoRouteKind;
	public CargoRouteKind AcceptedCargoRouteKind => SupportedCargoRouteKind;
	public float TotalSize => dockedCapsule != null ? dockedCapsule.TotalSize : 0.0f;
	public float MaxSize => dockedCapsule != null ? dockedCapsule.MaxSize : 0.0f;
	public float FilledPercent => MaxSize <= 0.0f ? 0.0f : (TotalSize / MaxSize) * 100.0f;


	public bool TryDockCapsule(CargoCapsule capsule)
	{
		if (capsule == null || dockedCapsule != null || CanAcceptCargoRoute(capsule.RouteKind) == false)
			return false;

		dockedCapsule = capsule;
		dockedCapsule.OnInvalidated += HandleDockedCapsuleInvalidated;
		capsule.SetCurrentDock(this);
		capsule.transform.SetParent(transform, false);
		capsule.transform.localPosition = Vector3.zero;
		OnDockedCapsuleChanged();
		OnCapsuleDocked?.Invoke(this);

		dockedCapsule.OnQuantityChanged += OnCapsuleQuantityChanged;

		return true;
	}

	public bool CanAcceptCargoRoute(CargoRouteKind routeKind) => SupportedCargoRouteKind == routeKind;

	public bool TryUndockCapsule(out CargoCapsule capsule)
	{
		capsule = null;

		if (dockedCapsule == null)
			return false;

		dockedCapsule.OnQuantityChanged -= OnCapsuleQuantityChanged;
		dockedCapsule.OnInvalidated -= HandleDockedCapsuleInvalidated;

		capsule = dockedCapsule;
		OnBeforeCapsuleUndocked(capsule);
		dockedCapsule = null;
		capsule.SetCurrentDock(null);
		capsule.transform.SetParent(null, true);

		OnDockedCapsuleChanged();
		OnCapsuleUndocked?.Invoke(this);

		return true;
	}

	public bool IsCapsuleEmpty()
	{
		if (dockedCapsule == null)
			return true;

		return dockedCapsule.Stacks.Count == 0;
	}

	public override bool GetBox(out BoxBase box)
	{
		box = null;
		if (CanGetBox() == false || TryUndockCapsule(out CargoCapsule capsule) == false)
			return false;

		box = capsule;
		return true;
	}

	public override bool PutBox(BoxBase box)
	{
		if (CanPutBox() == false || box is not CargoCapsule capsule)
			return false;

		return TryDockCapsule(capsule);
	}

	public override bool CanGetBox()
	{
		return HasCapsule;
	}

	public override bool CanPutBox()
	{
		return HasCapsule == false;
	}

	public override void OnPositionSet(in Unity.Mathematics.int3 pos, FacingDirection direction)
	{
		enabled = true;
		position = pos;
		facingDirection = direction;
	}

	public override void OnDestroyedBy(in DestroyContext ctx)
	{
		if (ctx.IsOverride == false)
			return;

		CargoCapsule capsule = dockedCapsule;
		if (capsule != null && GameContext.HasInstance)
			GameContext.Instance.BoxMgr?.DestroyBox(capsule);
	}

	public override void OnRemoved()
	{
	}

	public virtual bool CanUserRemove(out FacilityRemovalFailure failure)
	{
		if (HasCapsule)
		{
			failure = new FacilityRemovalFailure(
				FacilityRemovalFailureReason.ContainsCapsule,
				"Undock the capsule before removing this facility.");
			return false;
		}

		failure = FacilityRemovalFailure.None;
		return true;
	}

	protected virtual void OnDockedCapsuleChanged()
	{
	}

	protected virtual void OnBeforeCapsuleUndocked(CargoCapsule capsule)
	{
	}

	protected virtual void OnCapsuleQuantityChanged()
	{
	}

	private void HandleDockedCapsuleInvalidated(BoxBase box)
	{
		if (box != dockedCapsule)
			return;

		TryUndockCapsule(out _);
	}
}
