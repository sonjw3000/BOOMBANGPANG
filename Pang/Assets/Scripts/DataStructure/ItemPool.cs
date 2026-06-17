using System;
using System.Collections.Generic;
using UnityEngine;

public class GameObjectPool
{
	private readonly List<GameObject> items;
	private readonly Func<GameObject> factory;

	public GameObjectPool(int preload, Func<GameObject> factory)
	{
		items = new(preload);
		this.factory = factory;

		for (int i = 0; i < preload; ++i)
		{
			var obj = factory();
			if (obj == null)
				continue;

			obj.SetActive(false);
			items.Add(obj);
		}
	}

	public GameObject Get()
	{
		GameObject item = null;

		for (int i = 0; i < items.Count; ++i)
		{
			if (items[i].activeSelf == false)
			{
				item = items[i];
				break;
			}
		}

		if (item == null)
		{
			item = factory();
			if (item == null)
				return null;

			items.Add(item);
		}

		item.SetActive(true);
		return item;
	}

	public void Release(GameObject item)
	{
		if (item == null)
			return;

		item.SetActive(false);
	}

	public void ReleaseAll()
	{
		foreach (var obj in items)
		{
			if (obj == null)
				continue;

			obj.SetActive(false);
		}
	}
}


public class ItemPool<T>
{
	private readonly Stack<T> stack;
	private readonly Func<T> factory;

	public ItemPool(int preload, Func<T> factory)
	{
		stack = new Stack<T>(preload);
		this.factory = factory;
	}

	public T Get()
	{
		if (stack.Count > 0)
			return stack.Pop();
		else
			return factory();
	}

	public void Release(T item)
	{
		stack.Push(item);
	}

}
