using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class Rocket : ShelfBase
{
	[SerializeField] private float fallingSpeed = 5.0f;
	[SerializeField] private int3 landingPoint;
	private Vector3 forwardVector = new Vector3(0, 1, 0);

	private RocketManager RocketMgr => GameContext.Instance.RocketMgr;

	private DeliveryService DeliveryService => GameContext.Instance.DeliveryService;
	public int3 LandingPos => landingPoint;


	public void Update()
	{
		// land rocket
		transform.position += forwardVector * fallingSpeed * Time.deltaTime;
		
		if (transform.position.y <= landingPoint.y)
		{
			transform.position = new Vector3(
				LandingPos.x,
				LandingPos.y,
				LandingPos.z
				);
			RocketMgr.OnRocketLanding(this);
			//gameObject.SetActive(false);
		}
	}

	public void InitializePosition(in int3 landingPoint, in Vector3 forwardVector, float fallingSpeed)
	{
		this.landingPoint = landingPoint;
		this.forwardVector = forwardVector.normalized;
		this.fallingSpeed = fallingSpeed;
	}


	public void SetupPayloadByDelivery()
	{
		while (true)
		{
			if (DeliveryService.TryPeek(out var request) == false)
				break;

			int added = AddItem(request.TargetItem.ItemID, request.Quantity);

			if (added != request.Quantity)
			{
				// can't handle the whole request
				request.ReduceAmount(added);
				break;
			}

			DeliveryService.AcceptDelivery();
		}
		
	}

	public void SetupPayload(List<ItemStack> payload)
	{
		stacks = payload;
	}

	public List<ItemStack> GetPayload()
	{
		return stacks;
	}

}
