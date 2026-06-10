using UnityEngine;

public sealed class BuildingSelectionProxy : MonoBehaviour
{
	private BuildingManager buildingManager;
	private Building building;

	public BuildingManager BuildingManager => buildingManager;
	public Building Building => building;

	public void Bind(BuildingManager manager, Building targetBuilding)
	{
		buildingManager = manager;
		building = targetBuilding;
		name = targetBuilding != null ? $"BuildingSelection_{targetBuilding.DisplayName}" : "BuildingSelection";
	}

	public bool IsBoundTo(Building targetBuilding) => building == targetBuilding;
}
