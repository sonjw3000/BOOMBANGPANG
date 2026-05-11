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
			if (zoneManager.AddZone(preset.DisplayName, preset.ZoneType, preset.Bounds, preset.Floor) == null)
			{
				Debug.LogWarning($"Failed to create worker spawn zone: {preset.DisplayName}");
			}
		}
	}
}
