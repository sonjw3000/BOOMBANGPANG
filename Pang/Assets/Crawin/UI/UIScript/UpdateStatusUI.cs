using UnityEditor;
using UnityEngine;

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
	bool mInit;
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
        Robot = transform.GetChild(0).gameObject;
        Shelf = transform.GetChild(1).gameObject;
		Viewport = new Transform[2];
		Viewport[0] = Shelf.transform.GetChild(0);
		Viewport[1] = Robot.transform.GetChild(0);

		if (MousePicking)
        {
            mPicking = MousePicking.GetComponent<Picking>();
        }
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
}
