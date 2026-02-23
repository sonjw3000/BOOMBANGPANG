
// 돈, 평판, 기타등등

public class EconomyManager
{
	private int money;
	private float reputation;

	private int montlyIncome;
	private int montlyOutcome;

	public int Money => money;
	public float Reputation => reputation;

	public void AddMoney(int amount)
	{
		money += amount;
	}

	public void SpendMoney(int amount)
	{
		money -= amount;
	}

	public void ChangeReputation(float amount)
	{
		reputation += amount;
	}
}
