

[System.Serializable]
public struct WorkerBaseStatDefinition
{
	[UnityEngine.Range(0.01f, 1.5f)] public float baseMoveSpeedMultiplier;
	[UnityEngine.Range(0.01f, 1.5f)] public float minimumMoveSpeedMultiplier;
	[UnityEngine.Range(0.01f, 1.5f)] public float baseWorkSpeedMultiplier;
	[UnityEngine.Range(0.01f, 1.5f)] public float minimumWorkSpeedMultiplier;
}
