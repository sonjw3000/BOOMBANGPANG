using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class TilemapGenerator : MonoBehaviour
{
	private GameObject tileParent;
	private GameObject robotParent;
	//private Resources resources;
	private Resources resources => GameContext.Instance.MapResources;


	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		//resources = GameObject.Find("Resources").GetComponent<Resources>();
		if (resources == null)
		{
			Debug.LogError("no Resources Object");
		}
		else
		{
			GenerateMap();
		}
	}

	void ApplyTemplateToInstance(GameObject inst, int prefabIdx)
	{
		if (!resources.IndexToMaterials.ContainsKey(prefabIdx))
		{
			Debug.Log("ERROR no such index :" + prefabIdx);
			return;
		}

		var rendererTemplate = resources.IndexToMaterials[prefabIdx];

		var root = inst.transform;

		foreach (var template in rendererTemplate.pathToMaterials)
		{
			Transform tr = string.IsNullOrEmpty(template.Key) ? root : root.Find(template.Key);
			if (!tr) continue;

			var r = tr.GetComponent<Renderer>();
			if (!r) continue;

			r.sharedMaterials = template.Value;
		}

		inst.isStatic = false;
	}

	// Update is called once per frame
	void Update()
	{
		//printMap();
	}

	void clearObjectParents()
	{
		if (tileParent != null)
		{
			DestroyImmediate(tileParent);
		}
		else
		{
			GameObject old = GameObject.Find("TileParent");
			if (old != null)
			{
				DestroyImmediate(old);
			}
		}
		tileParent = new GameObject("TileParent");
		tileParent.transform.parent = transform;

		if (robotParent != null)
		{
			DestroyImmediate(robotParent);
			Debug.Log("robotParent Delete");
		}
		else
		{
			GameObject old = GameObject.Find("RobotParent");
			if (old != null)
			{
				DestroyImmediate(old);
			}
		}
		robotParent = new GameObject("RobotParent");
		robotParent.transform.parent = transform;
	}

	void GenerateMap()
	{
		clearObjectParents();

		Cell[,,] map = resources.mapRef;
		if (map == null)
		{
			Debug.Log("너가 문제였구나");
		}
		GameObject[] Prefabs = resources.Prefabs;
		int3 mapSize = resources.mapSize;
		// X*Z 크기의 커다란 QUAD 하나 생성 및 배치
		GameObject Tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
		Tile.transform.parent = tileParent.transform;

		Tile.transform.localScale = new Vector3Int(mapSize.x, mapSize.z, mapSize.y);
		Tile.transform.Rotate(90, 0, 0);
		Tile.transform.position = new Vector3(mapSize.x / 2 - 0.5f, 0, mapSize.z / 2 - 0.5f); // Y값 수정 요망 09.28  ( 층수가 다른 quad 배치해야함 )

		// query every materials used by map
		Dictionary<string, Material> toBeInstancedMaterials = new();

		foreach (var prefab in Prefabs)
		{
			var root = prefab.transform;
			var renderers = prefab.GetComponentsInChildren<Renderer>(true);
			var list = new List<Renderer>(renderers.Length);

		}



		//for (int y = 0; y < mapSize.y; ++y)
		//{
		//    for(int x = 0; x < mapSize.x; ++x)
		//    {
		//        for(int z = 0; z < mapSize.z; ++z)
		//        {
		//            map[x, y, z].type = 0;
		//            Vector3 pos = new Vector3(x+0.5f, Prefabs[0].transform.position.y, z+0.5f);
		//            map[x, y, z].obj = Instantiate(Prefabs[0], pos, Prefabs[0].transform.rotation, tileParent.transform);
		//            map[x, y, z].obj.name = $"{x},{y},{z}";
		//        }
		//    }
		//}

		// 맵에 빌딩 배치
		foreach (ObjectData buildingData in resources.mapJsonRef.buildingData)
		{
			map[buildingData.x, buildingData.y, buildingData.z].type = buildingData.type;
			List<int3> coord = map[buildingData.x, buildingData.y, buildingData.z].GetBuildRange();
			foreach (int3 delta in coord) // 이 부분은 1x1 이상의 크기인 빌딩일 때 작동
			{
				int nx = buildingData.x + delta.x;
				int ny = buildingData.y + delta.y;
				int nz = buildingData.z + delta.z;

				if (nx >= 0 && nx < mapSize.x &&
					ny >= 0 && ny < mapSize.y &&
					nz >= 0 && nz < mapSize.z)
				{
					map[nx, ny, nz].type = buildingData.type;
				}
			}
			Vector3 pos = new Vector3(buildingData.x, Prefabs[buildingData.type].transform.position.y, buildingData.z);
			quaternion baseRot = Prefabs[buildingData.type].transform.rotation * Quaternion.Euler(0, 90 * buildingData.head, 0);

			var inst = Instantiate(Prefabs[buildingData.type], pos, baseRot, tileParent.transform);
			ApplyTemplateToInstance(inst, buildingData.type);
			map[buildingData.x, buildingData.y, buildingData.z].obj = inst;
		}
		resources.mapJsonRef.buildingData.Clear();

		// 맵에 로봇 배치
		foreach (ObjectData robotData in resources.mapJsonRef.robotdata)
		{
			Vector3 pos = new Vector3(robotData.x, Prefabs[robotData.type].transform.position.y, robotData.z);
			quaternion baseRot = Prefabs[robotData.type].transform.rotation * Quaternion.Euler(0, 90 * robotData.head, 0);

			map[robotData.x, robotData.y, robotData.z].obj = Instantiate(Prefabs[robotData.type], pos, baseRot, robotParent.transform);
			Status status = map[robotData.x, robotData.y, robotData.z].obj.GetComponent<Status>();
			status.SetInit(map[robotData.x, robotData.y, robotData.z].obj.name, robotData.type);
			map[robotData.x, robotData.y, robotData.z].type = robotData.type;

			// Shelf도 지금 Robotdata로 배치되고 있어서 일단 이곳에 작성 -> 아직 모델들 정리가 안됨
			Shelf shelf = map[robotData.x, robotData.y, robotData.z].obj.GetComponent<Shelf>();
			if (shelf)
			{
				int3 pickingPosition = shelf.PickingPosition;
				map[pickingPosition.x, pickingPosition.y, pickingPosition.z].type = -1;	// -1이란? 로봇은 이동 가능하지만 배치는 불가능한 위치

			}

		}
		resources.mapJsonRef.robotdata.Clear(); // 딕셔너리로 다 옮겼으니 초기화하자
		printMap();
	}

	void printMap()
	{
		Cell[,,] map = resources.mapRef;
		string field = "";
		for (int y = 0; y < resources.mapSize.y; ++y)
		{
			for (int z = 0; z < resources.mapSize.z; ++z)
			{
				for (int x = 0; x < resources.mapSize.x; ++x)
				{
					field += map[x, y, z].type + " ";
				}
				field += "\n";
			}
		}
		Debug.Log(field);
	}
}
