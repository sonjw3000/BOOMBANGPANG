using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameContext))]
class GameContextEditor : Editor
{
	SerializedProperty itemsDB;
	SerializedProperty inventory;

	bool itemFoldout = true;
	bool inventoryFoldout = true;

	private void OnEnable()
	{
		itemsDB = serializedObject.FindProperty("itemDB");
		inventory = serializedObject.FindProperty("itemInventoryData");
	}

	public override void OnInspectorGUI()
	{
		//DrawDefaultInspector();

		GameContext manager = (GameContext)target;

		serializedObject.Update();

		itemFoldout = EditorGUILayout.Foldout(itemFoldout, "item");
		if (itemFoldout)
		{
			EditorGUILayout.Space(4);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(itemsDB, true);
			EditorGUI.indentLevel--;
		}

		inventoryFoldout = EditorGUILayout.Foldout(inventoryFoldout, "Shelf & Inventory");
		if (inventoryFoldout)
		{
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(inventory, true);

			ItemInventory invData = manager.ItemInventoryData;

			foreach(ShelfBase shelf in invData.Containers)
			{
				if (shelf == null)
					continue;
				EditorGUILayout.Space(4);
				EditorGUILayout.LabelField($"Shelf: {shelf.name}", EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EditorGUILayout.LabelField($"Picking Position: {shelf.PickingPosition}");
				EditorGUILayout.LabelField("Items:");
				EditorGUI.indentLevel++;

				foreach(var item in shelf.Items)
				{
					EditorGUILayout.LabelField($"Item ID: {item.Key}, Quantity: {item.Value.Quantity}");
				}
				EditorGUI.indentLevel--;
				EditorGUI.indentLevel--;
			}
		}

		if (GUILayout.Button("테스트용 랙에 아이템 배치"))
		{
			manager.TestStoreItem();
		}

		if (GUILayout.Button("랙에 아이템 채우기"))
		{
			manager.TestFullStockItems();
		}
	}
}
