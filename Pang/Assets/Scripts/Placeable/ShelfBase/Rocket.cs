using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Rocket : ShelfBase
{
	[SerializeField] private float fallingSpeed = 5.0f;
	[SerializeField] private int3 landingPoint;
	private Vector3 forwardVector = new Vector3(0, 1, 0);

	private RocketManager RocketMgr => GameContext.Instance.RocketMgr;

	public int3 LandingPos => landingPoint;

	protected override void SetInteractionPoints()
	{
		Vector3 projOnFloor = forwardVector;
		projOnFloor.y = 0;
		projOnFloor = projOnFloor.normalized;

		interactionPoints.Add(new int3(
			Mathf.RoundToInt(GridPosition.x + projOnFloor.x),
			Mathf.RoundToInt(GridPosition.y),
			Mathf.RoundToInt(GridPosition.z + projOnFloor.z)
			));
	}

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

	public void SetupPayload(List<ItemStack> payload)
	{
		stacks = payload;
	}

	public List<ItemStack> GetPayload()
	{
		return stacks;
	}

}
