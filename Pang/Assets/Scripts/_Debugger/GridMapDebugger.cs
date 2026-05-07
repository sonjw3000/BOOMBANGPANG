using Unity.Mathematics;
using UnityEditor;
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

	static private Color defaultColor = new(1f, 1f, 1f, 0.12f);
	static private Color reserveColor = new(0f, 1f, 0f, 0.55f);

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

#if UNITY_EDITOR
		GUIStyle style = new GUIStyle();
		style.fontSize = 10;
		style.normal.textColor = Color.black;
		style.alignment = TextAnchor.UpperLeft;
#endif

		int3 gridSize = gridService.MapSize;

		for (int x = 0; x < gridSize.x; ++x)
		{
			for (int y = 0; y < gridSize.y; ++y)
			{
				for (int z = 0; z < gridSize.z; ++z)
				{
					Vector3 world = new Vector3(x, y, z);
					Color res = GetColor(gridService.GetCell(new int3(x, y, z)));
					Gizmos.color = res;
					Gizmos.DrawCube(world, cellSize);

#if UNITY_EDITOR
					if (res != reserveColor) continue;

					// 큐브 좌상단 위치 계산
					Vector3 labelPos =
						world
						+ new Vector3(
							-cellSize.x * 0.45f,
							 cellSize.y * 0.45f,
							-cellSize.z * 0.45f);

					Handles.Label(labelPos,
						$"({x},{y},{z})",
						style);
#endif
				}
			}
		}

	}

	private static Color GetColor(GridCell cell)
	{
		if (cell.ReservedRoute != null) return reserveColor;
		if (cell.IsBlocked) return new Color(1f, 0f, 0f, 0.45f);
		if (cell.Flags.HasFlag(GridFlags.Interaction)) return new Color(0f, 1f, 1f, 0.45f);
		return defaultColor;
	}
}
