using AYellowpaper.SerializedCollections;
using System.Collections.Generic;
using UnityEngine;

public partial class WorkPolicyService
{
	public WorkPolicyRuntimeSaveData CaptureState()
	{
		EnsureRuntimeMultipliers();
		WorkPolicyRuntimeSaveData data = new();

		foreach (WorkerPolicyType workerPolicyType in System.Enum.GetValues(typeof(WorkerPolicyType)))
		{
			data.MoveSpeedMultipliers.Add(new WorkerPolicyTypeFloatSaveData
			{
				WorkerPolicyType = workerPolicyType,
				Value = GetMoveSpeedMultiplier(workerPolicyType),
			});
			data.WorkSpeedMultipliers.Add(new WorkerPolicyTypeFloatSaveData
			{
				WorkerPolicyType = workerPolicyType,
				Value = GetWorkSpeedMultiplier(workerPolicyType),
			});
		}

		return data;
	}

	public void RestoreState(WorkPolicyRuntimeSaveData data)
	{
		ResetRuntimeState();

		if (data == null)
			return;

		ApplyWorkerPolicyTypeValues(moveSpeedMultipliers, data.MoveSpeedMultipliers);
		ApplyWorkerPolicyTypeValues(workSpeedMultipliers, data.WorkSpeedMultipliers);
	}

	public void ResetRuntimeState()
	{
		EnsureRuntimeMultipliers();

		foreach (WorkerPolicyType workerPolicyType in System.Enum.GetValues(typeof(WorkerPolicyType)))
		{
			moveSpeedMultipliers[workerPolicyType] = DefaultSpeedMultiplier;
			workSpeedMultipliers[workerPolicyType] = DefaultSpeedMultiplier;
		}
	}

	private static void ApplyWorkerPolicyTypeValues(SerializedDictionary<WorkerPolicyType, float> target, List<WorkerPolicyTypeFloatSaveData> values)
	{
		if (values == null)
			return;

		foreach (WorkerPolicyTypeFloatSaveData entry in values)
		{
			if (entry == null)
				continue;

			float value = entry.Value;
			target[entry.WorkerPolicyType] = Mathf.Clamp(value <= 0.0f ? DefaultSpeedMultiplier : value, MinimumSpeedMultiplier, MaximumSpeedMultiplier);
		}
	}
}
