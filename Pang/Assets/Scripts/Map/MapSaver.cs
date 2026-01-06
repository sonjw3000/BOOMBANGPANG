using System.Collections.Generic;
using Unity.Mathematics;

public class MapSaver
{
	private readonly int3 mapSize;
	private readonly Cell[,,] cells;
	private readonly List<IGridPlaceable> placeables;

	public MapSaver(int3 mapSize, Cell[,,] cells, List<IGridPlaceable> placeables)
	{
		this.mapSize = mapSize;
		this.cells = cells;
		this.placeables = placeables;
	}

	public void Save()
	{

	}
}
