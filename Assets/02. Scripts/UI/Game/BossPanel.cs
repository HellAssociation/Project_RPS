using System.Collections;
using SystemEnums;
using TMPro;
using UnityEngine;

/// <summary>
/// 매 라운드 10번째 적(보스) 등장을 3초간 크게 보여주는 패널. 호스트가 보스 싸움 시작 전
/// ServerBroadcastBossIntro로 통보하면 모든 클라가 동시에 표시합니다. (효과는 현재 placeholder)
/// </summary>
public class BossPanel : PanelBase
{
    #region [Function] Inheritance
    public override EUIType UIType => EUIType.Boss;
    #endregion

    [SerializeField] TextMeshProUGUI titleTMP;
    [SerializeField] TextMeshProUGUI descriptionTMP;

    const float SHOW_SECONDS = 3f;

    NetworkManager Network => App.SystemManager.Network;

    Coroutine _showCoroutine;

    void OnEnable()  => Network.OnBossIntro += HandleBossIntro;
    void OnDisable() => Network.OnBossIntro -= HandleBossIntro;

    void HandleBossIntro(int round)
    {
        if (titleTMP != null) titleTMP.text = "BOSS";
        if (descriptionTMP != null) descriptionTMP.text = BuildDescription(round);

        OpenPanel();

        if (_showCoroutine != null) StopCoroutine(_showCoroutine);
        _showCoroutine = StartCoroutine(HideAfterDelay());
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(SHOW_SECONDS);
        ClosePanel();
        _showCoroutine = null;
    }

    static string BuildDescription(int round)
    {
        DataManager data = App.Data.BaseData;
        if (data != null && data.TryGetEnemy(EEnemyType.ENEMY_TYPE_10, out EnemyData boss) && !string.IsNullOrEmpty(boss.code))
            return $"Round {round} - {boss.code}";

        return $"Round {round}";
    }
}
