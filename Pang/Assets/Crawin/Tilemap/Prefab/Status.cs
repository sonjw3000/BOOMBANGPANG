using Unity.Mathematics;
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
		if (m_status == null)
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

	public void GetStatus(Transform Viewport, bool init)
	{
		m_status.GetStatus(Viewport, gameObject, init);
	}

	public void SetInit(string name, int id)
	{
		CheckAllocate();
		m_status.SetName(name);
		m_status.SetId(id);
	}
	public void SetID(int id)
	{
		CheckAllocate();
		m_status.SetId(id);
	}
	public void SetName(string name)
	{
		CheckAllocate();
		m_status.SetName(name);
	}
	public int GetID()
	{
		return m_status.GetID();
	}

	public void SetGoal(int3 goal)
	{
		((RobotStatus)m_status).SetGoal(goal);
	}

	public void SetBattery(float battery)
	{
		((RobotStatus)m_status).SetBattery(battery);
	}

	public void SetBatteryEfficiency(float e)
	{
		((RobotStatus)m_status).SetBatteryEfficiency(e);
	}

	public void DecreaseBattery()
	{
		((RobotStatus)m_status).DecreaseBattery();
	}

	public void SetWeight(int weight)
	{
		((RobotStatus)m_status).SetWeight(weight);
	}

	public void SetMaxStorage(int maxStorage)
	{
		((RobotStatus)m_status).SetMaxStorage(maxStorage);
	}

}
