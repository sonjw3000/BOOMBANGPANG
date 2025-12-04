using UnityEngine;
using UnityEngine.UI;

public class UpdateStatusUI : MonoBehaviour
{
    GameObject Robot;
    GameObject Shelf;
    public GameObject MousePicking;
    private Picking mPicking;
    private GameObject mLastPickedObject;
    private Status mStatus;
	public GameObject InventoryPrefab;
	Transform[] Viewport;
	GameObject orderedItems;
	bool mInit;
	int orderedItemsSlotCnt;
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
        Robot = transform.GetChild(0).gameObject;
        Shelf = transform.GetChild(1).gameObject;
		Viewport = new Transform[3];
		Viewport[0] = Shelf.transform.GetChild(0).GetChild(0);
		Viewport[1] = Robot.transform.GetChild(0);
		orderedItems = Shelf.transform.GetChild(2).gameObject;
		Viewport[2] = orderedItems.transform.GetChild(0);
		if (MousePicking)
        {
            mPicking = MousePicking.GetComponent<Picking>();
        }
		orderedItemsSlotCnt = 0;
	}

    // Update is called once per frame
    void Update()
    {
		SyncCanvasState();
        UpdateCanvasInfo();
    }

	void SyncCanvasState()
    {
		if (mLastPickedObject != mPicking.SelectedObject)
		{
			mLastPickedObject = mPicking.SelectedObject;
			if (mLastPickedObject)
			{
				mStatus = mLastPickedObject.GetComponent<Status>();
				if (mStatus.IsRobot)
				{
					Robot.SetActive(true);
					Shelf.SetActive(false);
				}
				else
				{
					Shelf.SetActive(true);
					orderedItems.SetActive(false);
					Robot.SetActive(false);
				}
				mInit = true;
			}
			else
			{
				Robot.SetActive(false);
				Shelf.SetActive(false);
			}
		}
	}

    void UpdateCanvasInfo()
    {
		if (mLastPickedObject)
		{
			mStatus.GetStatus(Viewport[mStatus.IsRobot ? 1 : 0], mInit);
		}
		if (mInit)
			mInit = false;
	}

	public void SelectItemOnShelf()
	{
		var itemSet = GameContext.Instance.ItemDB.OrderedItems;
		int testcnt = 9;
        if (orderedItemsSlotCnt < itemSet.Count/*testcnt*/)
        {
			for (int i = orderedItemsSlotCnt; i < itemSet.Count/*testcnt*/; ++i)
			{
				GameObject child = new GameObject();
				child.transform.SetParent(Viewport[2].GetChild(0), false);
				Image img = child.AddComponent<Image>();
				//img.color = Color.black;
				float t = (float)(i - orderedItemsSlotCnt) / (itemSet.Count/*testcnt*/ - orderedItemsSlotCnt - 1); // 0 ~ 1

				// ∞À¡§ °Ê »Úªˆ¿∏∑Œ ¡°¡° π‡∞‘
				//img.color = Color.Lerp(Color.black, Color.white, t);
			}
			orderedItemsSlotCnt = itemSet.Count/*testcnt*/;
        }
        foreach (uint id in itemSet)
		{
			Debug.Log(id);
		}
		orderedItems.SetActive(true);
	}
}
