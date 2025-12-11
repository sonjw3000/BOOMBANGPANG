

using Unity.Mathematics;
using UnityEngine;

interface IGridPlaceable
{
	// grid actions
	public void OnPositionSet(Cell[,,] map, int3 position);
	public void OnReset(Cell[,,] map);
	public void OnDestoryedBy(Cell[,,] map, GameObject obj);

	public int3 GridPosition { get; }
}
