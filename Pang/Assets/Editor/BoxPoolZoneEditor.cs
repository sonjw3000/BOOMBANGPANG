using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BoxPoolService))]
class BoxPoolZoneEditor : Editor
{
	public static int Index;
	private Resources MapRes => GameContext.Instance.MapResources;
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		Index = (int)EditorGUILayout.IntField("Target Index", Index);

		BoxPoolService sys = (BoxPoolService)target;

		if (GUILayout.Button("Give Totebox"))
		{
			if (Index >= sys.BoxPoolZones.Count)
			{
				Debug.Log("Out Of Index!");
				return;
			}

			int idx = MapRes.FindPrefabIndexByName("Totebox");
			GameObject box = Instantiate(MapRes.Prefabs[idx]);
			sys.BoxPoolZones[Index].PutBox(box.GetComponent<BoxBase>());
		}
	}
}
