using System;
using System.Collections.Generic;
using UnityEngine;

public class UnitSelectPanel : MonoBehaviour
{
    [SerializeField] private Transform cardParent;   // 카드들이 붙을 부모
    [SerializeField] private UnitCardView cardPrefab;

    private Action<UnitData> onSelected;

    public void Open(List<UnitData> candidates, Action<UnitData> onSelected)
    {
        gameObject.SetActive(true);
        this.onSelected = onSelected;

        // 기존 카드 제거
        foreach (Transform child in cardParent)
        {
            Destroy(child.gameObject);
        }

        // 후보 3개로 카드 생성
        foreach (var data in candidates)
        {
            var card = Instantiate(cardPrefab, cardParent);
            card.Setup(data, OnClickCard);
        }
    }

    private void OnClickCard(UnitData data)
    {
        gameObject.SetActive(false);   // 패널 닫기
        onSelected?.Invoke(data);      // 선택된 UnitData 전달
    }
}
