using System;
using UnityEngine;

public enum TooltipChannel
{
    Biome,
    UnitSkill,
    Stat
}

public class TooltipSystem : MonoBehaviour
{
    public static TooltipSystem Instance { get; private set; }

    [Serializable]
    public struct ChannelUI
    {
        public TooltipChannel channel;
        public TooltipUI ui;
    }

    [SerializeField] private ChannelUI[] channelUIs;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        HideAll();                   
    }

    private TooltipUI GetUI(TooltipChannel channel)
    {
        if (channelUIs == null) return null;
        for (int i = 0; i < channelUIs.Length; i++)
            if (channelUIs[i].channel == channel)
                return channelUIs[i].ui;
        return null;
    }

    public void Show(TooltipChannel channel, string title, string body, string effect)
    {
        GetUI(channel)?.Show(title, body, effect);
    }

    public void Hide(TooltipChannel channel)
    {
        GetUI(channel)?.Hide();
    }

    public void HideAll()
    {
        if (channelUIs == null) return;
        foreach (var c in channelUIs)
            c.ui?.Hide();
    }
}