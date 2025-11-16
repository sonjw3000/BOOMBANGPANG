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

	public void SetBattery(int battery)
	{
		((RobotStatus)m_status).SetGoal(battery);
	}

	public void SetWeight(int weight)
	{
		((RobotStatus)m_status).SetWeight(weight);
	}
}
