using System.Collections.Generic;
using UnityEngine;
using static BoxPool;
using static WorkerTask;

public class BoxPoolManager : MonoBehaviour
{
	private Dictionary<BoxPoolType, List<BoxPool>> boxPoolZones = new();


	public void Start()
	{
		foreach (BoxPoolType type in System.Enum.GetValues(typeof(BoxPoolType)))
		{
			boxPoolZones[type] = new();
		}
	}
}
