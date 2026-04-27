using UnityEditor;
using UnityEngine;
using Unity.Mathematics;

[CustomEditor(typeof(GridService))]
class GridServiceEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		if (GUILayout.Button("Test Building Set"))
		{
			GridService gridService = (GridService)target;
			PlaceableCatalog catalog = GameContext.Instance.PlaceableCatalog;
		
			// robot
			for (int i = 0; i < 6; ++i)
			{
				PlacementContext ctxRobots = new(
					center: new int3(3 + i, 0, 2),
					dir: FacingDirection.North,
					def: catalog.FindById("PalletRobot_00")
				);
				gridService.OnInstall(ctxRobots);
			}

			// shelf
			PlacementContext ctxShelf = new(
					center: new int3(10, 0, 4),
					dir: FacingDirection.North,
					def: catalog.FindById("BoardShelf_00")
				);
			gridService.OnInstall(ctxShelf);

			// bp
			PlacementContext ctxBoxPool = new(
					center: new int3(1, 0, 1),
					dir: FacingDirection.North,
					def: catalog.FindById("BoxPool_00")
				);
			gridService.OnInstall(ctxBoxPool);

			// IB cargo port
			PlacementContext ctxIBCargoPort = new(
					center: new int3(6, 0, 8),
					dir: FacingDirection.North,
					def: catalog.FindById("IBCargoPort_00")
				);
			gridService.OnInstall(ctxIBCargoPort);

			// OB cargo port
			PlacementContext ctxOBCargoPort = new(
					center: new int3(8, 0, 8),
					dir: FacingDirection.North,
					def: catalog.FindById("OBCargoPort_00")
				);
			gridService.OnInstall(ctxOBCargoPort);

			// PackingStation
			PlacementContext ctxPackingStation = new(
					center: new int3(20, 0, 5),
					dir: FacingDirection.North,
					def: catalog.FindById("PackingStation_00")
				);
			gridService.OnInstall(ctxPackingStation);

			// LaunchStation
			PlacementContext ctxLaunchStation = new(
					center: new int3(30, 0, 15),
					dir: FacingDirection.North,
					def: catalog.FindById("LaunchStation_00")
				);
			gridService.OnInstall(ctxLaunchStation);
		}

	}

}