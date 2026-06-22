using UnityEngine;
using System;
using System.Collections.Generic;

public partial class OrderDeliveryService : MonoBehaviour
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
	private static BoxManager BoxMgr => GameContext.Instance.BoxMgr;

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

			BoxMgr.DisableBox(progress.Cargo);
			deliveryProgresses.RemoveAt(i);
		}
	}

	public void DeliverCargo(BoxBase box, float duration)
	{
		deliveryProgresses.Add(new(box, duration));
	}

}
