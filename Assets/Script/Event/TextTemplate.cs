using System.Collections.Generic;

public static class TextTemplate
{
    public static string Apply(string template, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(template)) return "";
        if (values == null) return template;

        string result = template;
        foreach (var kv in values)
            result = result.Replace("{" + kv.Key + "}", kv.Value ?? "");

        return result;
    }
}