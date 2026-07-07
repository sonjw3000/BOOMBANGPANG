using System;
using System.Collections.Generic;

namespace Assets.Scripts.Contract.ItemContract
{
	// 계약 만료시 결과를 기록하는 클래스
	// 월별 토탈 결과를 나중에 보여주어야할지 고려해보자
	public class ContractHistory
	{
		private readonly List<ContractRuntime> contractHistory = new();

		// 월별 토탈 결과? 일단 보류
		private readonly Dictionary<int, List<ContractRuntime>> contractHistoryPerMonth = new();

		public void AddContractResult(ContractRuntime contract, int month)
		{
			contractHistory.Add(contract);
		}

		public void RemoveContractResult(ContractRuntime contract)
		{
			contractHistory.Remove(contract);
		}
	}

}
