// EquipmentTooltipUI.cs

using System.Collections.Generic;
using System.Text;                 // ADDED
using UnityEngine;
using UnityEngine.UI;              // ADDED

public class EquipmentTooltipUI : MonoBehaviour
{
    [SerializeField] private RectTransform tooltipRoot;   // 툴팁 패널
    [SerializeField] private Transform content;          // VerticalLayoutGroup 붙은 Transform
    [SerializeField] private EquipmentTooltipLine linePrefab;

    [Header("List Options")]        // ADDED
    [SerializeField] private int maxGroupsToShow = 30;   // ADDED: 그룹(중복 묶인 항목) 최대 표시 개수
    [SerializeField] private ScrollRect scrollRect;      // ADDED: 있으면 맨위로 스크롤 올려줌

    // CHANGED: 단일 장비 대신 “장비 리스트” 표시
    public void ShowList(List<Equipment> equippedItems, Vector2 anchoredPos)
    {
        if (tooltipRoot == null || content == null || linePrefab == null) return;
        
        tooltipRoot.gameObject.SetActive(true);
        tooltipRoot.anchoredPosition = anchoredPos;

        // 기존 라인 제거
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        if (equippedItems == null || equippedItems.Count == 0)
        {
            var empty = Instantiate(linePrefab, content);
            empty.Set(null, "장착 중인 장비 없음");
            return;
        }

        // 1) 중복 묶기 (Equipment SO 레퍼런스로 그룹화)  
        var counts = new Dictionary<Equipment, int>();
        foreach (var eq in equippedItems)
        {
            if (eq == null) continue;
            counts.TryGetValue(eq, out int c);
            counts[eq] = c + 1;
        }

        // 2) 표시(최대 maxGroupsToShow까지만)    
        int shown = 0;
        int totalGroups = counts.Count;

        foreach (var kv in counts)
        {
            if (shown >= maxGroupsToShow) break;

            Equipment eq = kv.Key;
            int count = kv.Value;

            string inline = EquipmentStatFormatter.BuildInlineSummary(eq); //아래 Step 3에서 추가
            string title = eq != null ? eq.itemName : "(null)";
            string countSuffix = (count > 1) ? $"  x{count}" : "";

            // 한 줄(두 줄 텍스트)로 표현: "장검 x2\n힘 +5 / 힘 +5%"
            var sb = new StringBuilder();
            sb.Append(title).Append(countSuffix);
            if (!string.IsNullOrWhiteSpace(inline))
            {
                sb.Append('\n').Append(inline);
            }

            var row = Instantiate(linePrefab, content);
            row.Set(eq != null ? eq.icon : null, sb.ToString());

            shown++;
        }

        // 3) 더 남았으면 마지막에 안내 라인 추가         // ADDED
        int remaining = totalGroups - shown;
        if (remaining > 0)
        {
            var more = Instantiate(linePrefab, content);
            more.Set(null, $"… 외 {remaining}종 더 있음 (스크롤/상한 표시)");
        }

        // 4) 스크롤 맨 위로                           // ADDED
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;
        }

        // 5) 레이아웃 강제 갱신(툴팁 크기/정렬 안정)     // ADDED
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)content);
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRoot);
    }

    public void Hide()
    {
        if (tooltipRoot) tooltipRoot.gameObject.SetActive(false);
    }
    
}

