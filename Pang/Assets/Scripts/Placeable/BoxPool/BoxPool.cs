using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// box base를 보관하는 타일 단 하나

public class BoxPool : 
	BoxInteraction
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

		box.gameObject.transform.parent = null;

		box.gameObject.transform.localPosition = Vector3.zero;

		return true;
	}

	public override bool PutBox(BoxBase box)
	{
		if (boxes.Count >= maxStack)
			return false;

		boxes.Push(box);

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

	}

	public BoxPoolSaveData CaptureState()
	{
		BoxPoolSaveData data = new();
		foreach (var box in boxes)
		{
			if (box != null)
			{
				data.Boxes.Add(new BoxReferenceSaveData
				{
					BoxType = box.Type,
					BoxId = box.BoxId,
				});
			}
		}

		return data;
	}

	public void RestoreState(BoxPoolSaveData data)
	{
		boxes.Clear();
		if (data == null)
			return;

		for (int i = data.Boxes.Count - 1; i >= 0; i--)
		{
			BoxReferenceSaveData boxRef = data.Boxes[i];
			if (boxRef != null && GameContext.Instance.BoxMgr.TryGetBox(boxRef.BoxType, boxRef.BoxId, out var box))
				PutBox(box);
		}
	}
}
