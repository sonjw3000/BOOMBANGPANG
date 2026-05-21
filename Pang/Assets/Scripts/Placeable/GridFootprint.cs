using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct FootprintCell
{
	public GridFlags flags;
	public InteractionKind interactionKind;
}


[CreateAssetMenu(menuName = "Placeable/Grid Footprint")]
public sealed class GridFootprint : ScriptableObject
{
	[Header("Grid Footprint Settings")]
	[Min(1)] public int width = 1;
	[Min(1)] public int height = 1;

	[SerializeField] private Vector2Int pivot = new Vector2Int(0, 0);
	[SerializeField] private FootprintCell[] footprintCells;
	[SerializeField, HideInInspector] private bool isBlockingOutside = false;

	public Vector2Int Pivot => pivot;
	public FootprintCell Get(int x, int y) => footprintCells[y * width + x];
	public bool IsNeedToRefresh => isBlockingOutside;
	
	private void OnEnable()
	{
		RebuildBlockingOutsideFlag();
	}

	private void OnValidate()
	{
		RebuildBlockingOutsideFlag();
	}

	private void RebuildBlockingOutsideFlag()
	{
		isBlockingOutside = false;
		if (footprintCells == null)
			return;

		foreach (var cell in footprintCells)
		{
			if (cell.flags.HasFlag(GridFlags.SealsSpace))
			{
				isBlockingOutside = true;
				break;
			}
		}
	}

}
