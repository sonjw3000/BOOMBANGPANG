

using Unity.Mathematics;

interface IGridPlaceable
{
	// grid actions
	public void OnPositionSet(Cell[,,] map, int3 position);
	public void OnReset(Cell[,,] map);
	public int3 GridPosition { get; }
}
