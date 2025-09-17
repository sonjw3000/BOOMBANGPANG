
interface IPickable
{
	// todo, 이를 나중엔 ObjectID, size 등을 포함하는 구조체로 저장해야함
	string _CurrentCarrying { get; }
	int _PickupCapacity { get; }

	void OnCarryStart(string target);
	void OnCarryEnd(string target);

}