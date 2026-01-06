using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AIWorker))]
class AIWorkerEditor : Editor
{
	//private Resources MapRes => GameContext.Instance.MapResources;
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		AIWorker worker = (AIWorker)target;

		if (GUILayout.Button("Give Totebox"))
		{
			//int idx = MapRes.FindPrefabIndexByName("Totebox");
			//GameObject box = Instantiate(MapRes.Prefabs[idx]);

			//worker.TryAttachBox(box.GetComponent<ToteBox>());
		}
	}
}
