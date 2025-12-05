using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemInventory))]
class ItemInventoryEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		ItemInventory invData = (ItemInventory)target;

		foreach (ShelfBase shelf in invData.Containers)
		{
			if (shelf == null)
				continue;
			EditorGUILayout.Space(4);
			EditorGUILayout.LabelField($"Shelf: {shelf.name}", EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.LabelField($"Picking Position: {shelf.PickingPosition}");
			EditorGUILayout.LabelField("Items:");
			EditorGUI.indentLevel++;

			foreach (var item in shelf.Items)
			{
				EditorGUILayout.LabelField($"Item ID: {item.Key}, Quantity: {item.Value.Quantity}");
			}
			EditorGUI.indentLevel--;
			EditorGUI.indentLevel--;
		}

		if (GUILayout.Button("테스트용 랙에 아이템 배치"))
		{
			invData.TestStoreItem();
		}

		if (GUILayout.Button("랙에 아이템 채우기"))
		{
			invData.TestFullStockItems();
		}
	}
}
