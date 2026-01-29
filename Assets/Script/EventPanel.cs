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
        descText.text = def.description;

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
