using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WorkFlowManager))]
class WorkFlowManagerEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		WorkFlowManager manager = (WorkFlowManager)target;

		if (GUILayout.Button("테스트용 이동 작업 생성"))
		{
			manager.MakeTestPickingWork();
			//worker.SetMoveOn();
		}
	}
}
