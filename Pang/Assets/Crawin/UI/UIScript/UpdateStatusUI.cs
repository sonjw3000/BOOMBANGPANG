using TMPro;
using Unity.Mathematics;
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
        if (mLastPickedObject != mPicking.m_goSelectedObject) {
            mLastPickedObject = mPicking.m_goSelectedObject;
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

        if (mLastPickedObject)
        {
            if (mStatus.IsRobot)
            {
                int3 goal = mStatus.GetGoal();
                float battery = mStatus.GetBattery();
                int weight = mStatus.GetWeight();
                int max_storage = mStatus.GetMaxStorage();

                Transform Viewport = Robot.transform.GetChild(0);

                foreach(Transform element in Viewport)
                {
                    if(element.name == "Goal")
                    {
                        TextMeshProUGUI text = element.GetChild(0).GetChild(1).GetComponent<TextMeshProUGUI>();
                        string s = "(" + goal.x + ", " + goal.z + ")";
                        text.text = s;

                        Image percent = element.GetChild(1).GetChild(0).GetComponent<Image>();
                        FindRoute fr = mLastPickedObject.GetComponent<FindRoute>();
                        percent.fillAmount = fr.GetPathPercent();
                    }
                    else if(element.name == "Battery")
                    {
						Image percent = element.GetChild(1).GetChild(0).GetComponent<Image>();
                        percent.fillAmount = battery;
					}
                    else if(element.name == "Weight")
                    {
						Image percent = element.GetChild(1).GetChild(0).GetComponent<Image>();
						percent.fillAmount = (float)weight / max_storage;
					}
                }

            }
            else
            {

            }
        }
    }
}
