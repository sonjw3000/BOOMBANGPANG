using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// box base를 보관하는 타일 단 하나

public class BoxPool : MonoBehaviour
{
	[SerializeField] private int maxStack = 50;
	private int3 position;
	private Stack<BoxBase> boxes = new();

	public int3 Position => position;

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
}
