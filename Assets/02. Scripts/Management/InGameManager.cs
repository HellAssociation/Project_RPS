using SystemEnums;
using UnityEngine;

/// <summary>
/// 인게임 씬 매니저. RPS 게임플레이 로직은 이후 이 클래스에 추가합니다.
/// </summary>
[DefaultExecutionOrder((int)EExecutionOrder.BaseManagement)]
public class InGameManager : SceneManagerBase
{
    public bool IsRoundActive { get; private set; }

    public void StartRound()
    {
        IsRoundActive = true;
        Debug.Log("[InGameManager] 라운드 시작");
    }

    public void EndRound()
    {
        IsRoundActive = false;
        Debug.Log("[InGameManager] 라운드 종료");
    }
}
