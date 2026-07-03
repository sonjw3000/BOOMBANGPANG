using System;
using UnityEditor;
using UnityEngine;
using static WorkerTask;

[CustomEditor(typeof(WorkerManager))]
class WorkerManagerEditor : Editor
{
	SerializedProperty childrenProp;
	bool foldout = true;

	public static int TestIdx;
	public static TaskType TestType;

	private void OnEnable()
	{
		childrenProp = serializedObject.FindProperty("workers");
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		TestType = (TaskType)EditorGUILayout.EnumPopup("Task Type", TestType);
		TestIdx = (int)EditorGUILayout.IntField("Target Worker Index",TestIdx);
		//DrawDefaultInspector();

		if (GUILayout.Button("Set Worker WorkType"))
		{
			WorkerManager workerMgr = (WorkerManager)target;

			workerMgr.ChangeWorkerTaskType(workerMgr.Workers[TestIdx], TestType);
		}

		if (GUILayout.Button("!!!!Set Test Workers!!!!"))
		{
			WorkerManager workerMgr = (WorkerManager)target;

			if (workerMgr.Workers.Count >= 6)
			{
				workerMgr.ChangeWorkerTaskType(workerMgr.Workers[0], TaskType.Unloading);
				workerMgr.ChangeWorkerTaskType(workerMgr.Workers[1], TaskType.Storing);
				workerMgr.ChangeWorkerTaskType(workerMgr.Workers[2], TaskType.Picking);
				workerMgr.ChangeWorkerTaskType(workerMgr.Workers[3], TaskType.Packing);
				workerMgr.SetWorkerAssignedTaskTypes(workerMgr.Workers[4], new[] { TaskType.PackingInput, TaskType.PackingOutput });
				workerMgr.ChangeWorkerTaskType(workerMgr.Workers[5], TaskType.Loading);
			}
		}

		foldout = EditorGUILayout.Foldout(foldout, "Workers");
		if (foldout)
		{
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(childrenProp, new GUIContent("Workers"));

			for (int i = 0; i < childrenProp.arraySize; i++)
			{
				var element = childrenProp.GetArrayElementAtIndex(i);
				var obj = element.objectReferenceValue as AIWorker;

				if (obj == null) continue;

				EditorGUILayout.Space(4);
				EditorGUILayout.LabelField($"Worker {i}: {obj.name}", EditorStyles.boldLabel);
				using (new EditorGUI.IndentLevelScope())
				{
					EditorGUILayout.LabelField($"Worker ID: {obj.WorkerID}");
					EditorGUILayout.LabelField($"Current Task: {(obj.CurrentTask != null ? obj.CurrentTask.ShowStatus() : "None")}");
				}
			}
		}



	}
}
