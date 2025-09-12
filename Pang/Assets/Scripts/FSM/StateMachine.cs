

class StateMachine
{
	private BaseState _CurState;

	public void UpdateState()
	{
		if (_CurState != null)
			_CurState.OnUpdate();
	}

	public void ChangeState(BaseState state)
	{
		if (state == _CurState) return;
		if (_CurState != null) _CurState.OnExit();
		_CurState = state;
		_CurState.OnEnter();
	}
}
