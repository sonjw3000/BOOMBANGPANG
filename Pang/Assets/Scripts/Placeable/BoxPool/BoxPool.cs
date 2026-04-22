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

	static private WMSystem WMSys => GameContext.Instance.WMSys;

	public int CurrentBoxCount => boxes.Count;

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

		WMSys.BoxPoolMgr.Register(this);
	}

	public override void OnRemoved()
	{
		WMSys.BoxPoolMgr.Unregister(this);
	}

	public override void OnDestroyedBy(in DestroyContext ctx)
	{

	}
}
