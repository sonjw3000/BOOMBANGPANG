using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static UnityEditor.PlayerSettings;

// box base를 보관하는 타일 단 하나

public class BoxPool : 
	MonoBehaviour, 
	IGridPlaceable, 
	IGridPlacementEffect,
	IInteractionPoint
{
	[SerializeField] private int maxStack = 50;
	[SerializeField] private GameObject boxStackPos;
	[SerializeField] private float stackHeight = 0.2f;

	private int3 position;
	private Stack<BoxBase> boxes = new();

	private List<int3> interactionPoints = new();

	public IReadOnlyList<int3> InteractionPoints => interactionPoints;

	static private WMSystem WMSys => GameContext.Instance.WMSys;

	public int3 GridPosition => position;

	public bool GetBox(out BoxBase box)
	{
		box = null;
		if (boxes.Count == 0)
			return false;

		box = boxes.Pop();

		box.gameObject.transform.parent = null;

		box.gameObject.transform.localPosition = Vector3.zero;

		return true;
	}

	public bool PutBox(BoxBase box)
	{
		if (boxes.Count >= maxStack)
			return false;

		boxes.Push(box);

		box.gameObject.transform.parent = boxStackPos.transform;

		box.gameObject.transform.localPosition = Vector3.zero + new Vector3(0.0f, boxes.Count * stackHeight ,0.0f);

		return true;
	}

	public void OnPositionSet(in int3 position)
	{
		enabled = true;
		this.position = position;

		// 팔방으로 상호작용이 가능하다
		//for (int x = -1; x <= 1; ++x)
		//{
		//	for (int z = -1; z <= 1; ++z)
		//	{
		//		if (x == 0 && z == 0) continue;

		//		GridMap[position.x + x, position.y, position.z + z].type = -1;
		//	}
		//}

		Debug.Log("BoxPool Added!!");

		WMSys.BoxPoolMgr.RegisterPool(this);
	}

	public void OnRemoved()
	{
		foreach (int3 interPos in interactionPoints)
		{
			//GridMap[interPos.x, interPos.y, interPos.z].type = 0;
		}

		WMSys.BoxPoolMgr.UnRegisterPool(this);
	}

	public void OnDestroyedBy(in DestroyContext ctx)
	{

	}
}
