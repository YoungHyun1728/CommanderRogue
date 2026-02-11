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

    private EventDefinition current;
    private Action<EventDefinition, EventChoice> onPick;

    public void Open(EventDefinition def, Action<EventDefinition, EventChoice> onPick)
    {
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
            var b = Instantiate(buttonPrefab, buttonsRoot);
            b.GetComponentInChildren<TextMeshProUGUI>().text = choice.buttonText;
            b.onClick.AddListener(() => this.onPick?.Invoke(current, choice));
        }
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
