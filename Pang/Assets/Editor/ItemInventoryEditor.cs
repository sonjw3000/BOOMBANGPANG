using System.Collections.Generic;
using UnityEditor;

[CustomEditor(typeof(ShelfStorageService))]
class ItemInventoryEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		ShelfStorageService invData = (ShelfStorageService)target;

		if (UnityEngine.GUILayout.Button("테스트용 랙에 아이템 배치"))
		{
			invData.TestStoreItem();
		}

		foreach (ShelfBase shelf in invData.Containers)
		{
			if (shelf == null)
				continue;
			EditorGUILayout.Space(4);
			EditorGUILayout.LabelField($"Shelf: {shelf.name}", EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.LabelField($"Picking Position: {shelf.InteractionPoints[0]}");

			// item totals
			EditorGUILayout.LabelField("ItemTotals and Reserved Quantities:");

			EditorGUI.indentLevel++;
			foreach (var item in shelf.ItemTotals)
			{
				EditorGUILayout.LabelField($"Item ID: {item.Key}, Total Quantity: {item.Value}, Reserved: {shelf.ItemToBePicked.GetValueOrDefault(item.Key)}");
			}
			EditorGUI.indentLevel--;

			// item stacks
			EditorGUILayout.LabelField("Stacks:");
			EditorGUI.indentLevel++;
			foreach (var item in shelf.Stacks)
			{
				EditorGUILayout.LabelField($"Item ID: {item.ItemID}, Quantity: {item.Quantity}");
			}
			EditorGUI.indentLevel--;

			EditorGUI.indentLevel--;
			EditorGUI.indentLevel--;
		}



		//if (GUILayout.Button("랙에 아이템 채우기"))
		//{
		//	invData.TestFullStockItems();
		//}
	}
}
