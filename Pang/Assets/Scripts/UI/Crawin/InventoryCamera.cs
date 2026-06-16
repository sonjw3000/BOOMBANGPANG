using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryCamera : MonoBehaviour
{
	/*
	public Transform slotsParent;
	public GameObject slotPrefab;

	private Camera inventoryCamera;
	//private Resources resources;

	private GameContext GCtx => GameContext.Instance;
	//private Resources resources => GameContext.Instance.MapResources;

	private List<Texture2D> generatedTextures = new List<Texture2D>();

	[Header("Legacy Inventory")]
	public RawImage[] rawImages;
	public RenderTexture[] renderTextures;
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		//LegacyInventory();

		GeneratePreviews();
	}

	void LoadResources()
	{
		//resources = GameObject.Find("Resources").GetComponent<Resources>();
		inventoryCamera = GetComponent<Camera>();
	}

	void LegacyInventory()
	{
		LoadResources();
		int slotCnt = renderTextures.Length;

		int resourceCnt = resources.Prefabs.Length;
		GameObject[] previewInstances = new GameObject[resourceCnt];

		int InvLayer = LayerMask.NameToLayer("Inventory");
		for (int i = 0; i < resourceCnt; ++i)
		{
			previewInstances[i] = Instantiate(resources.Prefabs[i], GCtx.gameObject.transform);
			SetLayer(previewInstances[i].transform, InvLayer);
			previewInstances[i].SetActive(false);
		}

		for (int i = 0; i < slotCnt; ++i)
		{
			rawImages[i].texture = renderTextures[i];
			inventoryCamera.targetTexture = renderTextures[i];

			if (i < resourceCnt)
			{
				previewInstances[i].SetActive(true);
				inventoryCamera.transform.position = previewInstances[i].transform.position + new Vector3(0, 2, -2);
				inventoryCamera.transform.LookAt(previewInstances[i].transform);
			}
			//Debug.Log(i + "번째 찰칵");
			inventoryCamera.Render();

			if (i < resourceCnt)
				previewInstances[i].SetActive(false);
		}

		inventoryCamera.targetTexture = null;
		inventoryCamera.enabled = false;
	}

	void SetLayer(Transform parent, int layer)
	{
		parent.gameObject.layer = layer;
		foreach (Transform child in parent)
		{
			SetLayer(child, layer);
		}

	}

	// Update is called once per frame
	void Update()
	{

	}

	void GeneratePreviews()
	{
		LoadResources();
		// Resource 안에 존재하는 prefab들 칸 생성
		int resourceCnt = resources.Prefabs.Length;
		int InvLayer = LayerMask.NameToLayer("Inventory");

		// 갯수만큼 생성 i가 2부터인 이유는 0은 빈칸, 1은 기둥 고정
		for (int i = 2; i < resourceCnt; i++)
		{
			// (1) 프리팹 임시 생성
			GameObject instance = Instantiate(resources.Prefabs[i], GCtx.gameObject.transform);
			SetLayer(instance.transform, InvLayer);
			instance.SetActive(true);

			// (2) 카메라 세팅
			inventoryCamera.transform.position = instance.transform.position + new Vector3(0, 2, -2);
			inventoryCamera.transform.LookAt(instance.transform);

			// (3) RenderTexture 생성
			RenderTexture rt = new RenderTexture(256, 256, 16);
			inventoryCamera.targetTexture = rt;

			// (4) 렌더링
			inventoryCamera.Render();

			instance.SetActive(false);

			// (5) Texture2D로 변환
			RenderTexture.active = rt;
			Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
			tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
			tex.Apply();

			// (6) UI에 적용
			GameObject slot = Instantiate(slotPrefab, slotsParent);
			slot.name = i.ToString();

			var component = slot.GetComponent<InsertPreviewPrefabsList>();
			if (component != null)
			{
				component.ID = i;
			}
			slot.GetComponent<RawImage>().texture = tex;

			generatedTextures.Add(tex);

			// (7) 정리
			RenderTexture.active = null;
			inventoryCamera.targetTexture = null;
			Destroy(rt);
			Destroy(instance);
		}

		//3의 배수만큼 남은 칸 생성
		// 3 -> 0 -> x
		// 4 -> 1 -> 2
		// 5 -> 2 -> 1
		// 6 -> 0 -> 0
		if (generatedTextures.Count % 3 != 0)
		{
			for (int i = 0; i < 3 - (generatedTextures.Count % 3); ++i)
			{
				GameObject slot = Instantiate(slotPrefab, slotsParent);
				slot.name = "EmptySlot";
				var component = slot.GetComponent<InsertPreviewPrefabsList>();
				if (component != null)
				{
					component.ID = int.MinValue;
				}
				slot.GetComponent<RawImage>().color = Color.clear;
			}
		}
		inventoryCamera.enabled = false;

		Debug.Log($" {generatedTextures.Count}개의 프리뷰 이미지 생성 완료!");
	}

	*/
}
