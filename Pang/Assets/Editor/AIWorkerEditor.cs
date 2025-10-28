using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Human))]
class AIWorkerEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		AIWorker worker = (AIWorker)target;

		if (GUILayout.Button("Move On"))
		{
			//worker.SetMoveOn();
		}
	}
}
