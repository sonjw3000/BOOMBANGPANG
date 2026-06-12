
public interface IBoundService
{
	public CargoPortService CargoPortService { get; }

	public void OnTaskCompleted(WorkerTask task);
}
