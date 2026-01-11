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
			item = factory();

		item.SetActive(true);
		items.Add(item);
		return item;
	}

	public void Release(GameObject item)
	{
		item.SetActive(false);
	}

	public void ReleaseAll()
	{
		foreach (var obj in items)
		{
			obj.SetActive(false);
		}
	}
}
