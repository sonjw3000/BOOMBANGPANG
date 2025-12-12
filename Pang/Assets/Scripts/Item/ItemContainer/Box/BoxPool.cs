using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// box base를 보관하는 타일 단 하나

public class BoxPool : MonoBehaviour, IGridPlaceable
{
	[SerializeField] private int maxStack = 50;
	private int3 position;
	private Stack<BoxBase> boxes = new();

	static private int PrefabIndex = GameContext.Instance.MapResources.FindPrefabIndexByName("BoxPool");
	static private Cell[,,] GridMap => GameContext.Instance.MapResources.mapRef;

	public int3 GridPosition => position;

	public bool GetBox(out BoxBase box)
	{
		box = null;
		if (boxes.Count == 0)
			return false;

		box = boxes.Pop();
		return true;
	}

	public bool PutBox(BoxBase box)
	{
		if (boxes.Count >= maxStack)
			return false;

		boxes.Push(box);
		return true;
	}

	public void OnPositionSet(int3 position)
	{
		this.position = position;
	}

	public void OnDestroyedBy(in DestroyContext ctx)
	{

	}
}
