using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(ZoneManager))]
public class ZoneManagerEditor : Editor
{
	[Serializable]
	private readonly struct WorkerSpawnAreaPreset
	{
		public readonly string DisplayName;
		public readonly ZoneType ZoneType;
		public readonly RectInt Bounds;
		public readonly int Floor;

		public WorkerSpawnAreaPreset(string displayName, ZoneType zoneType, RectInt bounds, int floor)
		{
			DisplayName = displayName;
			ZoneType = zoneType;
			Bounds = bounds;
			Floor = floor;
		}
	}

	private static readonly WorkerSpawnAreaPreset[] DefaultWorkerSpawnAreaPresets =
	{
		new(
			displayName: "Human Worker Spawn Area",
			zoneType: ZoneType.HumanSpawn,
			bounds: new RectInt(3, 4, 6, 2),
			floor: 0
		),
		new(
			displayName: "Robot Worker Spawn Area",
			zoneType: ZoneType.RobotSpawn,
			bounds: new RectInt(3, 2, 6, 2),
			floor: 0
		),
	};

	public override void OnInspectorGUI()
	{
		serializedObject.Update();
		DrawDefaultInspector();
		serializedObject.ApplyModifiedProperties();

		EditorGUILayout.Space();
		EditorGUILayout.HelpBox(
			"SetWorkerSpawnArea creates the default human/robot spawn areas and stores them as normal registered zones.",
			MessageType.Info
		);

		if (GUILayout.Button("SetWorkerSpawnArea"))
		{
			ZoneManager zoneManager = (ZoneManager)target;
			Undo.RegisterCompleteObjectUndo(zoneManager, "Set Worker Spawn Area");
			SetWorkerSpawnArea(zoneManager);
			PrefabUtility.RecordPrefabInstancePropertyModifications(zoneManager);
			EditorUtility.SetDirty(zoneManager);
			EditorSceneManager.MarkSceneDirty(zoneManager.gameObject.scene);
			serializedObject.Update();
			Repaint();
		}
	}

	private static void SetWorkerSpawnArea(ZoneManager zoneManager)
	{
		// Worker spawn presets must now live inside a building-owned zone.
		BuildingManager buildingManager = FindFirstObjectByType<BuildingManager>();
		BuildingFootprintService footprintService = FindFirstObjectByType<BuildingFootprintService>();
		if (buildingManager == null || footprintService == null)
		{
			Debug.LogWarning("SetWorkerSpawnArea requires BuildingManager and BuildingFootprintService in the scene.");
			return;
		}

		buildingManager.RebuildLookup();

		List<ZoneArea> zonesToRemove = new();
		foreach (var zone in zoneManager.RegisteredZones)
		{
			if (zone != null && zone.Type.IsWorkerSpawnZone())
				zonesToRemove.Add(zone);
		}

		foreach (var zone in zonesToRemove)
		{
			zoneManager.RemoveZone(zone);
		}

		foreach (var preset in DefaultWorkerSpawnAreaPresets)
		{
			if (TryFindOwningBuilding(buildingManager, footprintService, preset.Bounds, preset.Floor, out Building ownerBuilding) == false)
			{
				Debug.LogWarning($"Failed to find a building interior for worker spawn zone: {preset.DisplayName}");
				continue;
			}

			if (zoneManager.AddZone(ownerBuilding, preset.DisplayName, preset.ZoneType, preset.Bounds, preset.Floor) == null)
			{
				Debug.LogWarning($"Failed to create worker spawn zone: {preset.DisplayName}");
			}
		}
	}

	private static bool TryFindOwningBuilding(
		BuildingManager buildingManager,
		BuildingFootprintService footprintService,
		in RectInt bounds,
		int floor,
		out Building building)
	{
		building = null;
		if (buildingManager == null || footprintService == null)
			return false;

		foreach (BuildingFootprintRecord footprint in footprintService.RegisteredFootprints)
		{
			if (footprint == null || footprint.Floor != floor)
				continue;

			RectInt interior = new(footprint.Bounds.xMin + 1, footprint.Bounds.yMin + 1, footprint.Bounds.width - 2, footprint.Bounds.height - 2);
			if (interior.width <= 0 || interior.height <= 0)
				continue;

			if (ContainsRect(interior, bounds) == false)
				continue;

			if (buildingManager.TryGetBuilding(footprint.RuntimeBuildingId, out building) && building != null)
				return true;
		}

		return false;
	}

	private static bool ContainsRect(in RectInt outer, in RectInt inner)
	{
		return inner.xMin >= outer.xMin
			&& inner.yMin >= outer.yMin
			&& inner.xMax <= outer.xMax
			&& inner.yMax <= outer.yMax;
	}
}
