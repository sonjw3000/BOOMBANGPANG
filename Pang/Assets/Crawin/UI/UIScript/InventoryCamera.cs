using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class InventoryCamera : MonoBehaviour
{
    [Header("UI Slots")]
    public RawImage[] rawImages;
    public RenderTexture[] renderTextures;

    private Camera inventoryCamera;
    private GameObject[] previewInstances;
    private Resources resources;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        resources = GameObject.Find("Resources").GetComponent<Resources>();
        inventoryCamera = GetComponent<Camera>();

        //StartCoroutine(RenderInventoryPreviews());

        int slotCnt = renderTextures.Length;
        //yield return null; // 한 프레임 기다림

        int resourceCnt = resources.Prefabs.Length;
        previewInstances = new GameObject[resourceCnt];

        for (int i = 0; i < resourceCnt; ++i)
        {
            previewInstances[i] = Instantiate(resources.Prefabs[i], resources.transform);
            previewInstances[i].layer = LayerMask.NameToLayer("Inventory");
            previewInstances[i].SetActive(false);
        }

        for (int i = 0; i < slotCnt; ++i)
        {
            rawImages[i].texture = renderTextures[i];
            inventoryCamera.targetTexture = renderTextures[i];

            if (i < resourceCnt)
            {
                previewInstances[i].SetActive(true);
                inventoryCamera.transform.position = previewInstances[i].transform.position + new Vector3(0, 0, -3);
                inventoryCamera.transform.LookAt(previewInstances[i].transform);
            }
            inventoryCamera.Render();

            if (i < resourceCnt)
                previewInstances[i].SetActive(false);
        }

        //inventoryCamera.targetTexture = null;
    }

    IEnumerator RenderInventoryPreviews()
    {
        int slotCnt = renderTextures.Length;
        yield return null; // 한 프레임 기다림

        int resourceCnt = resources.Prefabs.Length;
        previewInstances = new GameObject[resourceCnt];

        for(int i = 0; i < resourceCnt; ++i)
        {
            previewInstances[i] = Instantiate(resources.Prefabs[i], resources.transform);
            previewInstances[i].layer = LayerMask.NameToLayer("Inventory");
            previewInstances[i].SetActive(false);
        }
        
        for (int i = 0; i < slotCnt; ++i)
        {
            rawImages[i].texture = renderTextures[i];
            inventoryCamera.targetTexture = renderTextures[i];

            if (i < resourceCnt)
            {
                previewInstances[i].SetActive(true);
                inventoryCamera.transform.position = previewInstances[i].transform.position + new Vector3(0, 0, -3);
                inventoryCamera.transform.LookAt(previewInstances[i].transform);
            }
            inventoryCamera.Render();

            if (i < resourceCnt)
                previewInstances[i].SetActive(false);
        }

        inventoryCamera.targetTexture = null;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
