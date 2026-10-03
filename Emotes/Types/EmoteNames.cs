using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Emotes;

public class EmoteNames
{
    private readonly IReadOnlyDictionary<string, CustomEmote> emotes;

    public EmoteNames(IReadOnlyDictionary<string, CustomEmote> emotes)
    {
        this.emotes = emotes ?? new Dictionary<string, CustomEmote>(StringComparer.OrdinalIgnoreCase);
    }

    public string OfEmote(CustomEmote emote)
    {
        if (emote == null) return "";
        return ResolveEmote(emote) ?? Capitalize(emote.Code);
    }

    public string OfCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return "";
        if (emotes.TryGetValue(code, out var emote)) return OfEmote(emote);

        var key = "emotes:emote-" + code;
        return Lang.HasTranslation(key) ? Lang.Get(key) : Capitalize(code);
    }

    public string OfCategory(string category)
    {
        if (string.IsNullOrEmpty(category)) return "";
        return ResolveCategory(category) ?? Capitalize(category);
    }

    public void ReportMissing(ILogger logger)
    {
        if (logger == null) return;

        foreach (var emote in emotes.Values)
        {
            if (ResolveEmote(emote) != null) continue;
            logger.Notification("Emote '{0}' from {1} has no display name, add lang key \"{2}\"",
                emote.Code, emote.Source, emote.Domain + ":emote-" + emote.Code);
        }

        foreach (var group in emotes.Values.GroupBy(e => e.Category))
        {
            if (ResolveCategory(group.Key) != null) continue;
            logger.Notification("Category '{0}' has no display name, add lang key \"{1}\"",
                group.Key, group.First().Domain + ":cat-" + group.Key);
        }
    }

    public static string Capitalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    private static string ResolveEmote(CustomEmote emote)
    {
        if (emote == null) return null;

        if (!string.IsNullOrEmpty(emote.Name))
        {
            if (!emote.Name.Contains(':')) return emote.Name;
            if (Lang.HasTranslation(emote.Name)) return Lang.Get(emote.Name);
        }

        var domainKey = emote.Domain + ":emote-" + emote.Code;
        if (Lang.HasTranslation(domainKey)) return Lang.Get(domainKey);

        var fallbackKey = "emotes:emote-" + emote.Code;
        if (Lang.HasTranslation(fallbackKey)) return Lang.Get(fallbackKey);

        return null;
    }

    private string ResolveCategory(string category)
    {
        if (string.IsNullOrEmpty(category)) return null;

        var members = emotes.Values.Where(e => e.Category == category).ToArray();

        var declared = members.Select(e => e.CategoryName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal);

        foreach (var name in declared)
        {
            if (!name.Contains(':')) return name;
            if (Lang.HasTranslation(name)) return Lang.Get(name);
        }

        var ownKey = "emotes:cat-" + category;
        if (Lang.HasTranslation(ownKey)) return Lang.Get(ownKey);

        var domains = members.Select(e => e.Domain)
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(d => d, StringComparer.Ordinal);

        foreach (var domain in domains)
        {
            var key = domain + ":cat-" + category;
            if (Lang.HasTranslation(key)) return Lang.Get(key);
        }

        return null;
    }
}
