using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EventPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private Transform buttonsRoot;
    [SerializeField] private Button buttonPrefab;
    [SerializeField] private GameObject choiceTooltipRoot;   // 고정 위치 패널(켜고 끄기)
    [SerializeField] private TextMeshProUGUI choiceTooltipText;

    private EventDefinition current;
    private Action<EventDefinition, EventChoice> onPick;

    public void Open(EventDefinition def, Action<EventDefinition, EventChoice> onPick)
    {
        if (choiceTooltipRoot != null)
            choiceTooltipRoot.SetActive(false);
        
        gameObject.SetActive(true);
        current = def;
        this.onPick = onPick;

        titleText.text = def.title;
        
        int cost = 0;
        if (EventManager.Instance != null)
        {
            foreach (var ch in def.choices)
            {
                cost = EventManager.Instance.GetPreviewGoldCost(ch);
                if (cost > 0) break;   // 비용 있는 첫 선택지
            }
        }
        descText.text = def.description.Replace("{GOLD}", cost.ToString());

        foreach (Transform c in buttonsRoot) Destroy(c.gameObject);

        foreach (var choice in def.choices)
        {
            var localChoice = choice;

            var b = Instantiate(buttonPrefab, buttonsRoot);
            b.GetComponentInChildren<TextMeshProUGUI>().text = choice.buttonText;
            // Hover tooltip 트리거
            var trigger = b.gameObject.AddComponent<EventChoiceTooltipTrigger>();
            trigger.Bind(this, localChoice);

            b.onClick.AddListener(() => this.onPick?.Invoke(current, choice));
        }
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void ShowChoiceTooltip(EventChoice choice)
    {
        if (choiceTooltipRoot == null || choiceTooltipText == null || choice == null) return;

        // hoverTooltip이 비어있으면 버튼 텍스트라도 보여주기(선택)
        var text = string.IsNullOrWhiteSpace(choice.hoverTooltip) ? choice.buttonText : choice.hoverTooltip;

        choiceTooltipText.text = text;
        choiceTooltipRoot.SetActive(true);
    }

    public void HideChoiceTooltip()
    {
        if (choiceTooltipRoot == null) return;
        choiceTooltipRoot.SetActive(false);
    }
}
