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

	[SerializeField] private FootprintCell[] footprintCells;


	public FootprintCell Get(int x, int y) => footprintCells[y * width + x];

}
