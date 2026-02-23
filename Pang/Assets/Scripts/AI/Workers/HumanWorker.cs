
public class HumanWorker : AIWorker
{
	private float experience;
	private float fatigue;

	public override void TickVitals(float deltaTime)
	{
		fatigue += deltaTime * 0.1f;
	}
}

