using UnityEngine;
using System;
using System.Collections.Generic;

public class OrderDeliveryService : MonoBehaviour
{
	private class DeliveryProgress
	{
		public BoxBase Cargo = null;
		public float TimeRemain = 0.0f;

		public DeliveryProgress(BoxBase box, float duration)
		{
			Cargo = box;
			TimeRemain = duration;
		}
	}

	private static OrderManager OrderMgr => GameContext.Instance.OrderMgr;

	private List<DeliveryProgress> deliveryProgresses = new();

	private void Update()
	{
		// reduce time remain
		for (int i = deliveryProgresses.Count - 1; i >= 0; --i)
		{
			var progress = deliveryProgresses[i];

			progress.TimeRemain -= Time.deltaTime;

			if (progress.TimeRemain > 0)
				continue;

			foreach (var stack in progress.Cargo.Stacks)
			{
				ItemPackage pkg = stack as ItemPackage;
				if (pkg == null)
					continue;

				pkg.ReportOutboundProgress(OrderMgr, PackageOutboundStage.Completed);
			}

			Debug.Log("Cargo Delivered!");

			GameContext.Instance.WMSys.BoxPoolService.ReturnToPool(progress.Cargo);
			deliveryProgresses.RemoveAt(i);
		}
	}

	public void DeliverCargo(BoxBase box, float duration)
	{
		deliveryProgresses.Add(new(box, duration));
	}

	public OrderDeliverySaveData CaptureState(Func<BoxBase, uint> registerBox)
	{
		OrderDeliverySaveData data = new();
		foreach (var progress in deliveryProgresses)
		{
			data.Progresses.Add(new DeliveryProgressSaveData
			{
				BoxId = registerBox != null ? registerBox(progress.Cargo) : 0,
				TimeRemain = progress.TimeRemain,
			});
		}

		return data;
	}

	public void RestoreState(OrderDeliverySaveData data, IReadOnlyDictionary<uint, BoxBase> restoredBoxes)
	{
		ResetRuntimeState();
		if (data == null || restoredBoxes == null)
			return;

		foreach (var progress in data.Progresses)
		{
			if (restoredBoxes.TryGetValue(progress.BoxId, out var cargo) == false)
				continue;

			deliveryProgresses.Add(new DeliveryProgress(cargo, progress.TimeRemain));
		}
	}

	public void ResetRuntimeState()
	{
		deliveryProgresses.Clear();
	}
}
