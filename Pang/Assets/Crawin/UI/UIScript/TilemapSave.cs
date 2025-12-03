using System;
using System.IO;
using UnityEngine;

public class TilemapSave : MonoBehaviour
{
	//private Resources resources;
	private Resources resources => GameContext.Instance.MapResources;
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		//resources = GameObject.Find("Resources").GetComponent<Resources>();
	}

	// Update is called once per frame
	void Update()
	{

	}

	public void ExportMap()
	{
		MapJson mapJson = new MapJson();
		mapJson.X = resources.mapSize.x;
		mapJson.Y = resources.mapSize.y;
		mapJson.Z = resources.mapSize.z;

		Cell[,,] map = resources.mapRef;
		if (map == null)
		{
			Debug.Log("map이 없네");
		}
		for (int y = 0; y < mapJson.Y; ++y)
		{
			for (int x = 0; x < mapJson.X; ++x)
			{
				for (int z = 0; z < mapJson.Z; ++z)
				{
					switch (map[x, y, z].type)
					{
						case -1:
						case 0:
						case int.MaxValue:
							//이동중인 출발 타일 위치이거나 통로이면 저장 x
							continue;
						default:
							int head = ((Mathf.RoundToInt(map[x, y, z].obj.transform.eulerAngles.y / 90f) % 4) + 4) % 4;
							ObjectData od = new ObjectData(x, y, z, map[x, y, z].type, head);
							if (map[x, y, z].type == 1)
							{
								mapJson.buildingData.Add(od);
							}
							else
							{
								mapJson.robotdata.Add(od);
							}
							break;
					}
				}
			}

			string outputPath = Path.Combine(Application.dataPath, "currentmap.json");
			string result = JsonUtility.ToJson(mapJson, true);
			File.WriteAllText(outputPath, result);
			Debug.Log("Export Json");
		}
	}
}
