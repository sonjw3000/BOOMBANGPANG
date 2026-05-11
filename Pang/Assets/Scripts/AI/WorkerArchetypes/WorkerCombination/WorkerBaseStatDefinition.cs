using UnityEngine;

[CreateAssetMenu(menuName = "Worker/BaseStat")]

public class WorkerBaseStatDefinition : ScriptableObject
{
	[Range(0.1f, 1.5f)] public float baseMoveSpeedMultiplier = 1.0f;
	[Range(0.1f, 1.5f)] public float minimumMoveSpeedMultiplier = 0.5f;
	[Range(0.1f, 1.5f)] public float baseWorkSpeedMultiplier = 1.0f;
	[Range(0.1f, 1.5f)] public float minimumWorkSpeedMultiplier = 0.5f;

	private void OnValidate()
	{
		if (baseMoveSpeedMultiplier < minimumMoveSpeedMultiplier)
			Debug.LogError("Min move speed should not bigger than base move speed");

		if (baseWorkSpeedMultiplier < minimumWorkSpeedMultiplier)
			Debug.LogError("Min work speed should not bigger than base work speed");
	}
}
