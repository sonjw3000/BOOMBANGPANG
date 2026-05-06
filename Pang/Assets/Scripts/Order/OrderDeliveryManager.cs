using UnityEngine;
using System.Collections.Generic;

public class OrderDeliveryManager : MonoBehaviour
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
				OrderMgr.ChangeOrderStatus(pkg.RelatedOrderLine, OrderStatus.Completed);
			}

			Debug.Log("Cargo Delivered!");

			Destroy(progress.Cargo.gameObject);
			deliveryProgresses.RemoveAt(i);
		}
	}

	public void DeliverCargo(BoxBase box, float duration)
	{
		deliveryProgresses.Add(new(box, duration));
	}
}
