using NUnit.Framework;
using Unity.Mathematics;
using System.Collections.Generic;
using UnityEngine;

public class Rocket : ShelfBase
{
	[SerializeField] private float fallingSpeed = 5.0f;
	[SerializeField] private int3 landingPoint;
	private Vector3 fowardVector = new Vector3(0, 1, 0);

	private RocketManager RocketMgr => GameContext.Instance.RocketMgr;

	public int3 LandingPos => landingPoint;

	protected override void SetPickingPosition()
	{
		Vector3 projOnFloor = fowardVector;
		projOnFloor.y = 0;
		projOnFloor = projOnFloor.normalized;

		interactionPoints.Add( new int3(
			Mathf.RoundToInt(GridPosition.x + projOnFloor.x),
			Mathf.RoundToInt(GridPosition.y),
			Mathf.RoundToInt(GridPosition.z + projOnFloor.z)
			));
	}

	public void Update()
	{
		// land rocket
		transform.position += fowardVector * fallingSpeed * Time.deltaTime;
		
		if (transform.position.y <= landingPoint.y)
		{
			RocketMgr.OnRocketLanding(this);
			//gameObject.SetActive(false);
		}
	}

	public void InitializePosition(int3 landingPoint, Vector3 fowardVector, float fallingSpeed)
	{
		this.landingPoint = landingPoint;
		this.fowardVector = fowardVector.normalized;
		this.fallingSpeed = fallingSpeed;
	}

	public void SetupPayload(Dictionary<uint, ItemStack> payload)
	{
		stacks = payload;
	}

	public Dictionary<uint, ItemStack> GetPayload()
	{
		return stacks;
	}

}
