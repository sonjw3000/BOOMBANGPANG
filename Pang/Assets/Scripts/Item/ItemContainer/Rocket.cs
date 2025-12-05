using Unity.Mathematics;
using UnityEngine;

public class Rocket : ShelfBase
{
	[SerializeField] private float fallingSpeed = 5.0f;
	[SerializeField] private int3 landingPoint;
	private Vector3 fowardVector = new Vector3(0, 1, 0);

	private RocketManager RocketMgr => GameContext.Instance.RocketMgr;

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

	// todo
	// List<ItemStack>의 형태로 바꾸기 (Payload라고 명명)
	public void SetupPayload(uint itemID, int quantity)
	{
		RegisterItem(itemID);
		items[itemID].Quantity = quantity;
	}

}
