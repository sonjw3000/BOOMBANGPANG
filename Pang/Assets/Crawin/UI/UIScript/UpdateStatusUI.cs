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
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Robot = transform.GetChild(0).gameObject;
        Shelf = transform.GetChild(1).gameObject;
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
			Transform Viewport;
			if (mStatus.IsRobot)
			{
				Viewport = Robot.transform.GetChild(0);
			}
			else
			{
				Viewport = Shelf.transform.GetChild(0);
			}
			mStatus.OnClick(Viewport);
		}
	}
}
