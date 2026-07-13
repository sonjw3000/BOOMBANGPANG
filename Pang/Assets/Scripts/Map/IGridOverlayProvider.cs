using System;
using UnityEngine;

public interface IGridOverlayProvider
{
	event Action OnGridOverlayRefreshRequested;
	bool HideZeroAlphaPixels { get; }

	bool TryFillGridOverlay(Color32[] buffer, int floor);
}
