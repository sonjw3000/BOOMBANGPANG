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

	public void OnClick()
	{
		m_status.GetStatus();
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
	public string GetName()
	{
		return m_status.GetName();
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

	public int3 GetGoal()
	{
		return ((RobotStatus)m_status).GetGoal();
	}
	public float GetBattery()
	{
		return ((RobotStatus)m_status).GetBattery();
	}
	public int GetWeight()
	{
		return ((RobotStatus)m_status).GetWeight();
	}

	public int GetMaxStorage()
	{
		return ((RobotStatus)m_status).GetMaxStorage();
	}
}
