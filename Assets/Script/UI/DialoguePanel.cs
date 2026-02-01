using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialoguePanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI speakerText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Image portraitImage;

    [Header("Choices")]
    [SerializeField] private Transform choicesRoot;
    [SerializeField] private Button choiceButtonPrefab;
    [SerializeField] private GameObject continueHint; // "Click/Space" 같은 안내

    private Action<int> onPickChoice;

    public void ShowNode(DialogueNode node, Action<int> onPickChoice)
    {
        gameObject.SetActive(true);
        this.onPickChoice = onPickChoice;

        if (speakerText) speakerText.text = node.speaker;
        if (bodyText) bodyText.text = node.text;

        if (portraitImage)
        {
            portraitImage.gameObject.SetActive(node.portrait != null);
            portraitImage.sprite = node.portrait;
        }

        ClearChoices();

        bool hasChoices = node.choices != null && node.choices.Count > 0;
        if (continueHint) continueHint.SetActive(!hasChoices);

        if (hasChoices)
        {
            for (int i = 0; i < node.choices.Count; i++)
            {
                int idx = i;
                var btn = Instantiate(choiceButtonPrefab, choicesRoot);

                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp) tmp.text = node.choices[i].buttonText;

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => this.onPickChoice?.Invoke(idx));
            }
        }
    }

     public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void ClearChoices()
    {
        if (!choicesRoot) return;
        for (int i = choicesRoot.childCount - 1; i >= 0; i--)
            Destroy(choicesRoot.GetChild(i).gameObject);
    }
}
