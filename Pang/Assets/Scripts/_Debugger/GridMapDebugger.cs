using Unity.Mathematics;
using UnityEngine;


public sealed class GridCellDebugOwner
{
	public Object StaticOwner;
	public Object DynamicOwner;
	public Object ReservationOwner;
}

public sealed class GridMapDebugger : MonoBehaviour
{
	[SerializeField] private GridService gridService;
	[SerializeField] private bool draw = false;
	[SerializeField] private float cubeHeight = 0.05f;
	[SerializeField] private Vector3 cellSize = new(1.0f, 0.05f, 1.0f);

	private void OnDrawGizmos()
	{
		if (draw == false)
			return;

		Draw();
	}

	private void OnDrawGizmosSelected()
	{
		if (draw == false)
			return;
		Draw();
	}

	private void Draw()
	{
		if (gridService == null || !gridService.IsReady) return;

		int3 gridSize = gridService.MapSize;

		for (int x = 0; x < gridSize.x; ++x)
		{
			for (int y = 0; y < gridSize.y; ++y)
			{
				for (int z = 0; z < gridSize.z; ++z)
				{
					Vector3 world = new Vector3(x, y, z);
					Gizmos.color = GetColor(gridService.GetCell(new int3(x, y, z)));
					Gizmos.DrawCube(world, cellSize);
				}
			}
		}

	}

	private static Color GetColor(GridCell cell)
	{
		if (cell.ReservedRoute != null) return new Color(0f, 1f, 0f, 0.55f);
		if (cell.IsBlocked) return new Color(1f, 0f, 0f, 0.45f);
		if (cell.Flags.HasFlag(GridFlags.Interaction)) return new Color(0f, 1f, 1f, 0.45f);
		return new Color(1f, 1f, 1f, 0.12f);
	}
}
