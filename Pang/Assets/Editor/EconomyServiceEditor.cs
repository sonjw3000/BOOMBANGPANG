using UnityEditor;

[CustomEditor(typeof(EconomyService))]
class EconomyServiceEditor : Editor
{

	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		EconomyService economy = (EconomyService)target;

		EditorGUILayout.LabelField($"Money: {economy.Money} \nRep: {economy.Reputation}");

	}
}
