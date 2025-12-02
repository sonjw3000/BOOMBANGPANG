using Unity.Mathematics;
using UnityEngine;

public class Rocket : ShelfBase
{
	[SerializeField] private Vector2 fallingTimeRange = new Vector2(3.0f, 7.0f);
	[SerializeField, Range(5.0f, 50.0f)] private float fallingSpeed = 5.0f;
	private int3 landingPoint;
	private Vector3 fowardVector = new Vector3(0, 1, 0);

	public int3 LandingPoint => landingPoint;

	public void Start()
	{
		// set random landing point near gamecontext's rocket landing zone
		var landingZone = GameContext.Instance.RocketLandingZoneCenter;
		var radius = GameContext.Instance.RocketLandingZoneRadius;

		int randomX = UnityEngine.Random.Range(-radius, radius + 1);
		int absX = Mathf.Abs(randomX);
		int randomZ = UnityEngine.Random.Range(-radius + absX, radius + 1 - absX);
		
		landingPoint = landingZone + new int3(randomX, 0, randomZ);

		float randomYRotation = UnityEngine.Random.Range(0.0f, 360.0f);
		Quaternion rot = Quaternion.Euler(20.0f, randomYRotation, 180.0f);
		transform.rotation = rot;

		// set position and direction
		fowardVector = rot * fowardVector;

		float randTime = UnityEngine.Random.Range(fallingTimeRange.x, fallingTimeRange.y);
		transform.position = new Vector3(landingPoint.x, landingPoint.y, landingPoint.z) - fowardVector * randTime;
	}

	public void Update()
	{
		// land rocket
		transform.position += fowardVector * fallingSpeed * Time.deltaTime;
		
		if (transform.position.y <= landingPoint.y)
		{
			transform.position = new Vector3(landingPoint.x, landingPoint.y, landingPoint.z);

			// 인바운드 뭐시기에 여기서 등록해주어야함

			enabled = false;
		}
	}

}
