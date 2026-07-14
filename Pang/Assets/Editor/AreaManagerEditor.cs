using UnityEditor;

[CustomEditor(typeof(AreaManager))]
public sealed class AreaManagerEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();
	}
}
