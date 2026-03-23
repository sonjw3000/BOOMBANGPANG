using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(OrderManager))]
class OrderManagerEditor : Editor
{
	//private Resources MapRes => GameContext.Instance.MapResources;
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		OrderManager mgr = (OrderManager)target;

		EditorGUILayout.LabelField($"Order Count: {mgr.Orders.Count}");
		EditorGUILayout.Space(4);
		EditorGUI.indentLevel++;
		foreach (var order in mgr.Orders)
		{
			EditorGUILayout.LabelField($"Order ID: {order.OrderID}");
			EditorGUI.indentLevel++;
			foreach (var line in order.Lines)
			{
				EditorGUI.indentLevel++;
				EditorGUILayout.LabelField($"Target Item ID/Quantity: {line.ItemID} / {line.Quantity} / {line.Status.ToString()}");
				EditorGUI.indentLevel--;
			}
			EditorGUI.indentLevel--;
		}

	}
}
