using UnityEditor;

[CustomEditor(typeof(ToteBox))]
class ToteBoxEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		ToteBox toteBox = (ToteBox)target;

		// print all items in box;

		EditorGUILayout.Space(4);
		EditorGUILayout.LabelField($"Current Size: {toteBox.TotalSize}/ Max: {toteBox.MaxSize}");

		EditorGUILayout.LabelField("Total Items");
		EditorGUI.indentLevel++;
		foreach (var item in toteBox.ItemTotals)
		{
			EditorGUILayout.LabelField($"Item ID: {item.Key}, Total Quantity: {item.Value}");
		}
		EditorGUI.indentLevel--;

	}
}
