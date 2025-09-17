

class StateMachine
{
	private BaseState CurState;

	public void UpdateState()
	{
		if (CurState != null)
			CurState.OnUpdate();
	}

	public void ChangeState(BaseState state)
	{
		if (state == CurState) return;
		if (CurState != null) CurState.OnExit();
		CurState = state;
		CurState.OnEnter();
	}
}
