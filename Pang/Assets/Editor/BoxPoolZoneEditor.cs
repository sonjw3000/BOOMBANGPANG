using UnityEditor;
using UnityEngine;
using Unity.Mathematics;

[CustomEditor(typeof(BoxPoolManager))]
class BoxPoolZoneEditor : Editor
{
	public static int Index;
	//private Resources MapRes => GameContext.Instance.MapResources;
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		BoxPoolManager sys = (BoxPoolManager)target;

		Index = (int)EditorGUILayout.IntField("Target Index", Index);
		if (GUILayout.Button("Give Tote Box"))
		{
			if (Index >= sys.PlaceableTargets.Count)
			{
				Debug.Log("Out Of Index!");
				return;
			}

			sys.GiveNewBox(sys.PlaceableTargets[Index], BoxType.Personal);
		}

		// box tracing
		foreach (BoxBase toteBox in sys.Boxes)
		{
			if (toteBox != null)
				continue;

			int3 pos = new((int)toteBox.transform.position.x,
							(int)toteBox.transform.position.y,
							(int)toteBox.transform.position.z);

			EditorGUILayout.Space(4);
			EditorGUILayout.LabelField($"BoxPos: {pos}");
			EditorGUI.indentLevel++;
			EditorGUILayout.LabelField($"Current Size: {toteBox.TotalSize}/ Max: {toteBox.MaxSize}");

			EditorGUILayout.LabelField("Total Items");
			EditorGUI.indentLevel++;
			foreach (var item in toteBox.ItemTotals)
			{
				EditorGUILayout.LabelField($"Item ID: {item.Key}, Total Quantity: {item.Value}");
			}
			EditorGUI.indentLevel--;
			EditorGUI.indentLevel--;

		}
	}
}
