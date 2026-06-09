using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스타포스 미니게임에서 한 명의 플레이어를 나타내는 별 하나.
/// 공유 트랙 위의 절대 anchoredPosition.x만 받아서 이동/정지하며,
/// 트랙·성공구역 계산은 StarforceModalContent가 전담한다.
/// </summary>
public class StarforceStar : MonoBehaviour
{
    const float STOPPED_ALPHA = 0.35f;

    [SerializeField] Image _image;
    [SerializeField] RectTransform _rect;

    public bool IsStopped { get; private set; }

    public void SetVisible(bool isVisible) => gameObject.SetActive(isVisible);

    /// <summary>색상 배정 + 위치/정지 상태 초기화.</summary>
    public void ResetStar(Color color)
    {
        IsStopped = false;
        color.a = 1f;
        _image.color = color;
    }

    public void MoveTo(float anchoredX)
    {
        if (IsStopped) return;
        SetPositionX(anchoredX);
    }

    public void Stop(float anchoredX)
    {
        if (IsStopped) return;

        IsStopped = true;
        SetPositionX(anchoredX);

        Color color = _image.color;
        color.a = STOPPED_ALPHA;
        _image.color = color;
    }

    void SetPositionX(float x)
    {
        _rect.anchoredPosition = new Vector2(x, _rect.anchoredPosition.y);
    }
}
