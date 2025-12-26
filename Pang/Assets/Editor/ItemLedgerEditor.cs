using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemLedger))]
public class ItemLedgerEditor : Editor
{
	public override void OnInspectorGUI()
	{
		ItemLedger itemLedger = (ItemLedger)target;

		EditorGUILayout.Space(4);
		EditorGUILayout.LabelField("TotalItems");
		EditorGUI.indentLevel++;
		foreach (var kvp in itemLedger.ItemTotals)
		{
			EditorGUILayout.LabelField($"ItemID: {kvp.Key}, Total: {kvp.Value}, Reserved: {itemLedger.ItemReserveds.GetValueOrDefault(kvp.Key)}");
		}
		EditorGUI.indentLevel--;

		EditorGUILayout.Space(4);
		EditorGUILayout.LabelField("Orderables");
		EditorGUI.indentLevel++;
		foreach (var kvp in itemLedger.OrderableItems)
		{
			EditorGUILayout.LabelField($"ItemID: {kvp}, Amount: {itemLedger.GetAvailable(kvp)}");
		}
		EditorGUI.indentLevel--;
	}
}
