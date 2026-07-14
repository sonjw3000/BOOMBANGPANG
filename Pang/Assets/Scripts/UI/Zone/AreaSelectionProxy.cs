using UnityEngine;

public class AreaSelectionProxy : MonoBehaviour
{
	private AreaManager areaManager;
	private Area area;

	public AreaManager AreaManager => areaManager;
	public Area Area => area;

	public void Bind(AreaManager manager, Area targetArea)
	{
		areaManager = manager;
		area = targetArea;
		name = targetArea != null ? $"AreaSelection_{targetArea.DisplayName}" : "AreaSelection";
	}

	public bool IsBoundTo(Area targetArea) => area == targetArea;
}
