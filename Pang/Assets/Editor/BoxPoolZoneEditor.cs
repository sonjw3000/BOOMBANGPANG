using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BoxPoolService))]
class BoxPoolZoneEditor : Editor
{
	public static int Index;
	//private Resources MapRes => GameContext.Instance.MapResources;
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

			sys.GiveNewBox(sys.BoxPoolZones[Index], BoxType.Personal);
		}
	}
}
