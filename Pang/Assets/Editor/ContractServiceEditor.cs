using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ContractService))]
class ContractServiceEditor : Editor
{
	public static int Index;
	public static int Duration;
	//private Resources MapRes => GameContext.Instance.MapResources;
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		Index = (int)EditorGUILayout.IntField("Target Contract Index", Index);
		Duration = (int)EditorGUILayout.IntField("Duration", Duration);

		ContractService contractService = (ContractService)target;

		if (GUILayout.Button("Add Contract"))
		{
			contractService.AddContract(Index, Duration);
		}
	}
}

