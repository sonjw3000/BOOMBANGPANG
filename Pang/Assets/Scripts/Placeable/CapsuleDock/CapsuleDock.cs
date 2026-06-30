using UnityEngine;

public abstract class CapsuleDock : BoxInteraction
{
	protected CargoCapsule dockedCapsule = null;

	public CargoCapsule DockedCapsule => dockedCapsule;
	public bool HasCapsule => dockedCapsule != null;
	public float TotalSize => dockedCapsule != null ? dockedCapsule.TotalSize : 0.0f;
	public float MaxSize => dockedCapsule != null ? dockedCapsule.MaxSize : 0.0f;
	public float FilledPercent => MaxSize <= 0.0f ? 0.0f : (TotalSize / MaxSize) * 100.0f;


	public bool TryDockCapsule(CargoCapsule capsule)
	{
		if (capsule == null || dockedCapsule != null)
			return false;

		dockedCapsule = capsule;
		capsule.SetCurrentDock(this);
		capsule.transform.SetParent(transform, false);
		capsule.transform.localPosition = Vector3.zero;
		OnDockedCapsuleChanged();

		dockedCapsule.OnQuantityChanged += OnCapsuleQuantityChanged;

		return true;
	}

	public bool TryUndockCapsule(out CargoCapsule capsule)
	{
		capsule = null;

		if (dockedCapsule == null)
			return false;

		dockedCapsule.OnQuantityChanged -= OnCapsuleQuantityChanged;

		capsule = dockedCapsule;
		OnBeforeCapsuleUndocked(capsule);
		dockedCapsule = null;
		capsule.SetCurrentDock(null);
		capsule.transform.SetParent(null, true);

		OnDockedCapsuleChanged();

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
	}

	public override void OnRemoved()
	{
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
}
