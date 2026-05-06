using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class Rocket : ShelfBase
{
	public enum RocketState
	{
		Landing,
		OnPad,
		Launching,
		Deactivated
	}

	[SerializeField] private float fallingSpeed = 5.0f;
	[SerializeField] private float launchSpeed = 10.0f;
	[SerializeField] private float launchHeight = 100.0f;
	[SerializeField] private int3 landingPoint;
	private Vector3 forwardVector = new Vector3(0, 1, 0);
	private RocketState state = RocketState.Landing;

	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.Rocket;

	private RocketManager RocketMgr => GameContext.Instance.RocketMgr;

	private DeliveryService DeliveryService => GameContext.Instance.DeliveryService;
	public int3 LandingPos => landingPoint;
	public RocketState State => state;


	public void Update()
	{
		switch (state)
		{
			case RocketState.Landing:
				UpdateLanding();
				break;
			case RocketState.Launching:
				UpdateLaunching();
				break;
		}
	}

	private void UpdateLanding()
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
			state = RocketState.OnPad;
			RocketMgr.OnRocketLanding(this);
			//gameObject.SetActive(false);
		}
	}

	private void UpdateLaunching()
	{
		transform.position += Vector3.up * launchSpeed * Time.deltaTime;

		if (transform.position.y >= launchHeight)
		{
			state = RocketState.Deactivated;
			RocketMgr.DisableRocket(this);
		}
	}

	public void Launch()
	{
		state = RocketState.Launching;
		this.enabled = true;
	}

	public void InitializePosition(in int3 landingPoint, in Vector3 forwardVector, float fallingSpeed)
	{
		this.landingPoint = landingPoint;
		this.forwardVector = forwardVector.normalized;
		this.fallingSpeed = fallingSpeed;
		this.state = RocketState.Landing;
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
