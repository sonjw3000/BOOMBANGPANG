using System.IO;
using Unity.Mathematics;
using UnityEngine;

public class SpawnRobots : MonoBehaviour
{
	public int NUMS;
	public enum SpawnType
	{
		Linear,
		Random
	}
	public SpawnType type;
	private Resources resources;
	public GameObject RobotPrefab;

	int robotID = 6;
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		resources = GameObject.Find("Resources").GetComponent<Resources>();
	}

	// Update is called once per frame
	void Update()
	{

	}

	public void Spawn()
	{
		Cell[,,] map = resources.mapRef;
		int3 mapSize = resources.mapSize;
		int leftCnt = NUMS;
		if (map == null)
		{
			Debug.Log("map이 없네");
		}

		while (leftCnt > 0)
		{
			for (int y = 0; y < mapSize.y; ++y)
			{
				for (int z = 0; z < mapSize.z; ++z)
				{
					for (int x = 0; x < mapSize.x; ++x)
					{
						if (map[x, y, z].type == 0)
						{
							if (type == SpawnType.Random)
							{
								if (UnityEngine.Random.Range(0, 2) == 0)
								{
									continue;
								}
							}
							Vector3 pos = new Vector3(x, y + RobotPrefab.transform.position.y, z);
							map[x, y, z].type = robotID;  // 지금 로봇 타입이 6번으로 고정되어있음;
							map[x, y, z].obj = Instantiate(RobotPrefab, pos, RobotPrefab.transform.rotation);
							map[x, y, z].obj.GetComponent<Status>().SetID(robotID);

							//FindRoute findroute = map[x, y, z].obj.GetComponent<FindRoute>();
							//findroute.type = robotID;
							//findroute.enabled = true;
							--leftCnt;
						}
						if (leftCnt == 0)
						{
							return;
						}
					}
				}
			}
		}
	}

	public void RemoveRobots()
	{
		Cell[,,] map = resources.mapRef;
		int3 mapSize = resources.mapSize;
		for (int y = 0; y < mapSize.y; ++y)
		{
			for (int z = 0; z < mapSize.z; ++z)
			{
				for (int x = 0; x < mapSize.x; ++x)
				{
					if (map[x, y, z].type == robotID || map[x, y, z].type == int.MaxValue)
					{
						map[x, y, z].type = 0;
						Destroy(map[x, y, z].obj);
						map[x, y, z].obj = null;
					}
				}
			}
		}
	}
}
