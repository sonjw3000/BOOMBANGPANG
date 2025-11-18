using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameContextEditor))]
class GameContextEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		GameContext manager = (GameContext)target;

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
