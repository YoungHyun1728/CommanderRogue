using System;
using System.Collections.Generic;
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

        // 템플릿이 없으면 버튼 텍스트라도 표시
        string template = string.IsNullOrWhiteSpace(choice.hoverTooltip)
            ? choice.buttonText
            : choice.hoverTooltip;
        
        var values = BuildChoiceTooltipValues(choice);

        choiceTooltipText.text = TextTemplate.Apply(template, values);
        choiceTooltipRoot.SetActive(true);
    }

    public void HideChoiceTooltip()
    {
        if (choiceTooltipRoot == null) return;
        choiceTooltipRoot.SetActive(false);
    }

    private Dictionary<string, string> BuildChoiceTooltipValues(EventChoice choice)
    {
        var dict = new Dictionary<string, string>();

        int round = (RunManager.Instance != null) ? RunManager.Instance.CurrentLevel : 1;
        dict["ROUND"] = round.ToString();

        // 1) 비용(스케일링 반영)
        int cost = (EventManager.Instance != null) ? EventManager.Instance.GetPreviewGoldCost(choice) : 0;
        dict["COST_GOLD"] = cost.ToString();
        dict["COST_GOLD_SIGNED"] = (cost > 0) ? ("-" + cost) : "0";

        // 2) 획득 골드(Outcome.addGold 기반) : 스케일링 반영 범위 계산
        // - Outcome이 여러 개/확률 분기여도 "가능한 최소~최대" 범위로 정확히 표시
        bool foundGoldGain = TryGetScaledGoldGainRange(choice, out int gainMin, out int gainMax);

        if (foundGoldGain)
        {
            dict["GAIN_GOLD_MIN"] = gainMin.ToString();
            dict["GAIN_GOLD_MAX"] = gainMax.ToString();

            // 동일하면 단일 값으로
            dict["GAIN_GOLD"] = (gainMin == gainMax) ? gainMin.ToString() : $"{gainMin}~{gainMax}";
        }
        else
        {
            dict["GAIN_GOLD_MIN"] = "0";
            dict["GAIN_GOLD_MAX"] = "0";
            dict["GAIN_GOLD"] = "0";
        }

        return dict;
    }

    private bool TryGetScaledGoldGainRange(EventChoice choice, out int minScaled, out int maxScaled)
    {
        minScaled = 0;
        maxScaled = 0;

        if (choice == null || choice.outcomes == null || choice.outcomes.Count == 0) return false;
        if (RunManager.Instance == null) return TryGetUnscaledGoldGainRange(choice, out minScaled, out maxScaled);

        bool found = false;
        int minAll = int.MaxValue;
        int maxAll = int.MinValue;

        foreach (var o in choice.outcomes)
        {
            if (o == null || !o.addGold) continue;

            int baseMin = Mathf.Min(o.goldMin, o.goldMax);
            int baseMax = Mathf.Max(o.goldMin, o.goldMax);

            float roundMul = (o.goldRoundMultiplier <= 0f) ? 1.10f : o.goldRoundMultiplier;

            int scaledMin = RunManager.Instance.GetScaledGoldAmount(baseMin, o.scaleGoldWithRound, roundMul);
            int scaledMax = RunManager.Instance.GetScaledGoldAmount(baseMax, o.scaleGoldWithRound, roundMul);

            // 혹시 역전될 일은 거의 없지만 방어
            int lo = Mathf.Min(scaledMin, scaledMax);
            int hi = Mathf.Max(scaledMin, scaledMax);

            minAll = Mathf.Min(minAll, lo);
            maxAll = Mathf.Max(maxAll, hi);
            found = true;
        }

        if (!found) return false;

        minScaled = minAll;
        maxScaled = maxAll;
        return true;
    }

    private bool TryGetUnscaledGoldGainRange(EventChoice choice, out int min, out int max)
    {
        min = 0; max = 0;

        bool found = false;
        int minAll = int.MaxValue;
        int maxAll = int.MinValue;

        foreach (var o in choice.outcomes)
        {
            if (o == null || !o.addGold) continue;

            int baseMin = Mathf.Min(o.goldMin, o.goldMax);
            int baseMax = Mathf.Max(o.goldMin, o.goldMax);

            minAll = Mathf.Min(minAll, baseMin);
            maxAll = Mathf.Max(maxAll, baseMax);
            found = true;
        }

        if (!found) return false;

        min = minAll;
        max = maxAll;
        return true;
    }
}
