using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class HPBarController : MonoBehaviour
{
    [Header("Delay Settings")]
    [SerializeField] float delaySeconds = 0.4f;   // 피격 후 딜레이 HP가 줄기 시작하는 대기시간
    [SerializeField] float drainSpeed   = 0.8f;   // 딜레이 HP가 닳는 속도 (초당 비율)

    Material _mat;

    float _currentFill  = 1f;
    float _delayedFill  = 1f;
    float _drainTimer   = 0f;   // 대기 타이머
    bool  _draining     = false;

    static readonly int PropFill    = Shader.PropertyToID("_FillAmount");
    static readonly int PropDelayed = Shader.PropertyToID("_DelayedFill");

    void Awake()
    {
        // Image마다 독립된 Material 인스턴스를 사용
        _mat = Instantiate(GetComponent<Image>().material);
        GetComponent<Image>().material = _mat;
    }

    void Update()
    {
        if (!_draining)
        {
            _drainTimer -= Time.deltaTime;
            if (_drainTimer <= 0f)
                _draining = true;
            return;
        }

        if (_delayedFill > _currentFill)
        {
            _delayedFill -= drainSpeed * Time.deltaTime;
            _delayedFill  = Mathf.Max(_delayedFill, _currentFill);
            _mat.SetFloat(PropDelayed, _delayedFill);
        }
    }

    /// <param name="current">현재 HP</param>
    /// <param name="max">최대 HP</param>
    public void SetHP(float current, float max)
    {
        float fill = Mathf.Clamp01(current / max);

        // HP가 줄었을 때만 딜레이 효과 발동
        if (fill < _currentFill)
        {
            _drainTimer = delaySeconds;
            _draining   = false;
        }

        _currentFill = fill;
        _mat.SetFloat(PropFill, _currentFill);

        // HP가 회복됐다면 딜레이도 즉시 맞춤
        if (fill > _delayedFill)
        {
            _delayedFill = fill;
            _mat.SetFloat(PropDelayed, _delayedFill);
        }
    }
}
