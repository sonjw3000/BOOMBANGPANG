using System.Collections.Generic;
using UnityEngine;

// box base를 보관하는 타일 단 하나

public class BoxPool : MonoBehaviour
{
	public enum BoxPoolType
	{
		None,
		// sotring
		Inbound,

		// wk
		Picking,

		// package
		Outbound,
	}

	[SerializeField] private int maxStack = 50;
	private BoxPoolType type = BoxPoolType.None;
	private Stack<BoxBase> boxes = new();

	public void OnInit(BoxPoolType type)
	{
		this.type = type; 
	}

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
