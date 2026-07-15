using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// box base를 보관하는 타일 단 하나

public partial class BoxPool : 
	BoxInteraction,
	IFacilityUserRemovalGuard
{
	[SerializeField] private int maxStack = 50;
	[SerializeField] private GameObject boxStackPos;
	[SerializeField] private float stackHeight = 0.2f;

	//private int3 position;
	private Stack<BoxBase> boxes = new();

	public int CurrentBoxCount => boxes.Count;
	public int MaxStackCount => maxStack;
	public IEnumerable<BoxBase> Boxes => boxes;

	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.BoxPool;
	//public int3 GridPosition => position;

	public override bool CanGetBox() => boxes.Count != 0;
	public override bool CanPutBox() => boxes.Count < maxStack;

	public override bool GetBox(out BoxBase box)
	{
		box = null;
		if (boxes.Count == 0)
			return false;

		box = boxes.Pop();
		box.OnInvalidated -= HandleStoredBoxInvalidated;

		box.gameObject.transform.parent = null;

		box.gameObject.transform.localPosition = Vector3.zero;

		return true;
	}

	public override bool PutBox(BoxBase box)
	{
		if (boxes.Count >= maxStack)
			return false;

		boxes.Push(box);
		box.OnInvalidated -= HandleStoredBoxInvalidated;
		box.OnInvalidated += HandleStoredBoxInvalidated;

		box.gameObject.transform.parent = boxStackPos.transform;

		box.gameObject.transform.localPosition = Vector3.zero + new Vector3(0.0f, boxes.Count * stackHeight ,0.0f);

		return true;
	}

	public override void OnPositionSet(in int3 position, FacingDirection direction)
	{
		enabled = true;
		this.position = position;
		this.facingDirection = direction;
	}

	public override void OnRemoved()
	{
	}

	public override void OnDestroyedBy(in DestroyContext ctx)
	{
		if (GameContext.HasInstance == false)
			return;

		BoxBase[] storedBoxes = boxes.ToArray();
		for (int i = 0; i < storedBoxes.Length; ++i)
		{
			if (storedBoxes[i] != null)
				GameContext.Instance.BoxMgr?.DestroyBox(storedBoxes[i]);
		}
	}

	public bool CanUserRemove(out FacilityRemovalFailure failure)
	{
		if (boxes.Count > 0)
		{
			failure = new FacilityRemovalFailure(
				FacilityRemovalFailureReason.ContainsBox,
				"Remove all boxes before removing this facility.");
			return false;
		}

		failure = FacilityRemovalFailure.None;
		return true;
	}

	private void HandleStoredBoxInvalidated(BoxBase box)
	{
		if (box == null || boxes.Contains(box) == false)
			return;

		box.OnInvalidated -= HandleStoredBoxInvalidated;
		BoxBase[] topToBottom = boxes.ToArray();
		boxes.Clear();
		for (int i = topToBottom.Length - 1; i >= 0; --i)
		{
			BoxBase stored = topToBottom[i];
			if (stored == null || stored == box)
				continue;

			boxes.Push(stored);
			stored.transform.localPosition = new Vector3(0.0f, boxes.Count * stackHeight, 0.0f);
		}
	}

}
