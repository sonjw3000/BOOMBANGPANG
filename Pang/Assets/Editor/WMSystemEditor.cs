using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WMSystem))]
class WMSystemEditor : Editor
{
	public static int Index;
	private Resources MapRes => GameContext.Instance.MapResources;
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		Index = (int)EditorGUILayout.IntField("Target Index", Index);

		WMSystem sys = (WMSystem)target;

		if (GUILayout.Button("Give Totebox"))
		{
			if (Index >= sys.BoxPoolMgr.BoxPoolZones.Count)
			{
				Debug.Log("Out Of Index!");
				return;
			}

			int idx = MapRes.FindPrefabIndexByName("Totebox");
			GameObject box = Instantiate(MapRes.Prefabs[idx]);
			sys.BoxPoolMgr.BoxPoolZones[Index].PutBox(box.GetComponent<BoxBase>());
		}
	}
}
