using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using DG.Tweening;

public class UnitCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image portrait;
    [SerializeField] private TextMeshProUGUI unitName;
    [SerializeField] private Button selectButton;
    [SerializeField] private CanvasGroup canvasGroup;   // <- attach on prefab root
    [SerializeField] private float idleAlpha = 0.5f;
    [SerializeField] private float hoverAlpha = 1f;
    
    [Header("카드 생성시 애니메이션")]
    [SerializeField] RectTransform content;   // 카드 내부 컨테이너
    [SerializeField] float spawnOffsetY = -120f;
    [SerializeField] float moveDuration = 0.25f;
    [SerializeField] float fadeDuration = 0.20f;

    private UnitData data;
    private Action<UnitData> onClick;
    private Action<UnitData> onHover;
    private Action onHoverExit;

    void OnEnable()
    {
        // 시작 상태
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, spawnOffsetY);
        canvasGroup.alpha = 0f;

        // 애니메이션
        content.DOAnchorPosY(0f, moveDuration).SetEase(Ease.OutQuad);
        canvasGroup.DOFade(idleAlpha, fadeDuration).SetEase(Ease.OutQuad);
    }

    public void Setup(UnitData data, Action<UnitData> onClick,
                      Action<UnitData> onHover, Action onHoverExit)
    {
        this.data = data;
        this.onClick = onClick;
        this.onHover = onHover;
        this.onHoverExit = onHoverExit;

        portrait.sprite = data.portrait;
        unitName?.SetText($"{data.unitName}");

        SetAlpha(idleAlpha);
        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => this.onClick?.Invoke(this.data));
    }

    public void OnPointerEnter(PointerEventData _) { SetAlpha(hoverAlpha); onHover?.Invoke(data); }
    public void OnPointerExit(PointerEventData _)  { SetAlpha(idleAlpha);  onHoverExit?.Invoke(); }

    private void SetAlpha(float a)
    {
        if (canvasGroup != null) canvasGroup.alpha = a;
    }
}