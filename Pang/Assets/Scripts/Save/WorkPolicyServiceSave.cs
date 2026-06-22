using AYellowpaper.SerializedCollections;
using System.Collections.Generic;
using UnityEngine;

public partial class WorkPolicyService
{
	public WorkPolicyRuntimeSaveData CaptureState()
	{
		EnsureRuntimeMultipliers();
		WorkPolicyRuntimeSaveData data = new();

		foreach (WorkerType workerType in System.Enum.GetValues(typeof(WorkerType)))
		{
			data.MoveSpeedMultipliers.Add(new WorkerTypeFloatSaveData
			{
				WorkerType = workerType,
				Value = GetMoveSpeedMultiplier(workerType),
			});
			data.WorkSpeedMultipliers.Add(new WorkerTypeFloatSaveData
			{
				WorkerType = workerType,
				Value = GetWorkSpeedMultiplier(workerType),
			});
		}

		return data;
	}

	public void RestoreState(WorkPolicyRuntimeSaveData data)
	{
		ResetRuntimeState();

		if (data == null)
			return;

		ApplyWorkerTypeValues(moveSpeedMultipliers, data.MoveSpeedMultipliers);
		ApplyWorkerTypeValues(workSpeedMultipliers, data.WorkSpeedMultipliers);
	}

	public void ResetRuntimeState()
	{
		EnsureRuntimeMultipliers();

		foreach (WorkerType workerType in System.Enum.GetValues(typeof(WorkerType)))
		{
			moveSpeedMultipliers[workerType] = DefaultSpeedMultiplier;
			workSpeedMultipliers[workerType] = DefaultSpeedMultiplier;
		}
	}

	private static void ApplyWorkerTypeValues(SerializedDictionary<WorkerType, float> target, List<WorkerTypeFloatSaveData> values)
	{
		if (values == null)
			return;

		foreach (WorkerTypeFloatSaveData entry in values)
		{
			if (entry == null)
				continue;

			float value = entry.Value;
			target[entry.WorkerType] = Mathf.Clamp(value <= 0.0f ? DefaultSpeedMultiplier : value, MinimumSpeedMultiplier, MaximumSpeedMultiplier);
		}
	}
}
