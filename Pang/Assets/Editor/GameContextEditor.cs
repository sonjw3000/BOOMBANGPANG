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
		DrawDefaultInspector();

		//GameContext manager = (GameContext)target;

		//serializedObject.Update();

		////itemFoldout = EditorGUILayout.Foldout(itemFoldout, "item");
		////if (itemFoldout)
		////{
		////	EditorGUILayout.Space(4);
		////	EditorGUI.indentLevel++;
		////	//EditorGUILayout.PropertyField(itemsDB, true);
		////	EditorGUI.indentLevel--;
		////}

		//inventoryFoldout = EditorGUILayout.Foldout(inventoryFoldout, "Shelf & Inventory");
		//if (inventoryFoldout)
		//{
		//	EditorGUI.indentLevel++;
		//	//EditorGUILayout.PropertyField(inventory, true);


		//}


	}
}
