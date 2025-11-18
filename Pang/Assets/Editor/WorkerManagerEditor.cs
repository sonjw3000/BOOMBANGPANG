using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WorkerManager))]
class WorkerManagerEditor : Editor
{
	SerializedProperty childrenProp;
	bool foldout = true;

	private void OnEnable()
	{
		childrenProp = serializedObject.FindProperty("workers");
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

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
		//DrawDefaultInspector();

		if (GUILayout.Button("Move On"))
		{
			//worker.SetMoveOn();
		}
	}
}
