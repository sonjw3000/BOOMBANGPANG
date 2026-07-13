using System;
using UnityEngine;

public interface IGridOverlayProvider
{
	event Action OnGridOverlayRefreshRequested;

	bool TryFillGridOverlay(Color32[] buffer, int floor);
}
