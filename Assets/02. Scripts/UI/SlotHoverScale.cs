using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public class SlotHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Scale Settings")]
    [Tooltip("Mouse hover size (ex : 1.2 => increase 20% size)")]
    protected float hoverScaleMultiplier = 1.2f;

    [Tooltip("size scale take time")]
    protected float scaleDuration = 0.12f;

    protected Vector3 originalScale = Vector3.zero;
    protected Tween scaleTween = null;
    
    protected virtual void Start()
    {
        originalScale = transform.localScale;
    }

    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        Play(originalScale * hoverScaleMultiplier, Ease.OutBack);
    }

    public virtual void OnPointerExit(PointerEventData eventData)
    {
        Play(originalScale, Ease.OutQuad);
    }

    private void Play(Vector3 _target, Ease _ease)
    {
        scaleTween?.Kill();
        scaleTween = transform.DOScale(_target, scaleDuration).SetEase(_ease).SetUpdate(true);
    }

    protected virtual void OnDisable()
    {
        scaleTween?.Kill();
        transform.localScale = originalScale;
    }
}
