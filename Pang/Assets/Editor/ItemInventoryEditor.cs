using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShelfStorageIndex))]
class ItemInventoryEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		ShelfStorageIndex invData = (ShelfStorageIndex)target;

		foreach (ShelfBase shelf in invData.Containers)
		{
			if (shelf == null)
				continue;
			EditorGUILayout.Space(4);
			EditorGUILayout.LabelField($"Shelf: {shelf.name}", EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.LabelField($"Picking Position: {shelf.InteractionPoints[0]}");
			EditorGUILayout.LabelField("Items:");
			EditorGUI.indentLevel++;

			foreach (var item in shelf.Stacks)
			{
				EditorGUILayout.LabelField($"Item ID: {item.ItemID}, Quantity: {item.Quantity}, Reserved: {item.Quantity - item.TobeQuantity}");
			}
			EditorGUI.indentLevel--;
			EditorGUI.indentLevel--;
		}

		//if (GUILayout.Button("테스트용 랙에 아이템 배치"))
		//{
		//	invData.TestStoreItem();
		//}

		//if (GUILayout.Button("랙에 아이템 채우기"))
		//{
		//	invData.TestFullStockItems();
		//}
	}
}
