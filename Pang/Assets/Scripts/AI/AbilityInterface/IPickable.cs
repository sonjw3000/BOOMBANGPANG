
interface IPickable
{
	// todo, 이를 나중엔 ObjectID, size 등을 포함하는 구조체로 저장해야함
	string currentCarrying { get; }
	int pickupCapacity { get; }

	void OnCarryStart(string target);
	void OnCarryEnd(string target);
}
