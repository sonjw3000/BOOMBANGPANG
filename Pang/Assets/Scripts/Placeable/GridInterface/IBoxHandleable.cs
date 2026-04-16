
public interface IBoxHandleable
{
	public bool CanGetBox();
	public bool CanPutBox();

	public bool GetBox(out BoxBase box);
	public bool PutBox(BoxBase box);
}
