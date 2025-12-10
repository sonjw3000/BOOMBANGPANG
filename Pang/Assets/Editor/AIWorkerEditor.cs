using Mono.Cecil;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AIWorker))]
class AIWorkerEditor : Editor
{
	private Resources MapRes => GameContext.Instance.MapResources;
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		AIWorker worker = (AIWorker)target;

		if (GUILayout.Button("Give Totebox"))
		{
			worker.TryGetComponent<CarryBoxAbility>(out var component);
			int idx = MapRes.FindPrefabIndexByName("Totebox");
			GameObject box = Instantiate(MapRes.Prefabs[idx]);
			component.TryAttachBox(box.GetComponent<ToteBox>());
		}
	}
}
