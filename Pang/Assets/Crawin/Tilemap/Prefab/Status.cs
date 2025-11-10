using UnityEngine;

public class Status : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public bool IsRobot;
    ObjectStatus m_status;
    void Start()
    {
        CheckAllocate();
        if (IsRobot && m_status.GetID() != 0)
            gameObject.AddComponent<FindRoute>();
    }

    void CheckAllocate()
    {
        if(m_status == null)
        {
            if (IsRobot)
            {
                m_status = new RobotStatus();
            }
            else
            {
                m_status = new ShelfStatus();
            }
        }
    }

    public void OnClick()
    {
        m_status.GetStatus();
    }
    public void SetID(int i)
    {
        CheckAllocate();
        m_status.SetId(i);
    }
    public int GetID()
    {
        return m_status.GetID();
    }
}
