using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;


public sealed class GridFootprint
{
	[Header("Grid Footprint Settings")]
	[SerializeField] private Vector2Int gridSize = new (1, 1);

	[Header("제로 베이스, 좌상단 0,0 우하단 x - 1,y - 1")]
	[SerializeField] private Vector2Int pivot = new(0, 0);

	
	public void GetCells(int3 origin, List<int3> cells)
	{
		cells.Clear();

		int minX = -pivot.x;
		int maxX = gridSize.x - 1 - pivot.x;

		int minY = -pivot.y;
		int maxY = gridSize.y - 1 - pivot.y;

		for (int x = minX; x <= maxX; ++x)
		{
			for (int y = minY; y <= maxY; ++y)
			{
				cells.Add(new int3(origin.x + x, origin.y, origin.z + y));
			}
		}
	}



}
