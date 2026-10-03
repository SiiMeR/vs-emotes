using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace Emotes;

public class EmoteCatalog
{
    public const string AllKey = "all";
    public const string FavoritesKey = "favorites";
    public const string VanillaKey = "vanilla";
    public const string MiscKey = "misc";

    private static readonly string[] VanillaFallback =
        { "wave", "cheer", "shrug", "cry", "nod", "facepalm", "bow", "laugh", "rage" };

    private readonly ICoreClientAPI capi;
    private readonly EmotesModSystem modSystem;
    private readonly EmoteFavorites favorites;

    public EmoteCatalog(ICoreClientAPI capi, EmotesModSystem modSystem, EmoteFavorites favorites)
    {
        this.capi = capi;
        this.modSystem = modSystem;
        this.favorites = favorites;
    }

    public List<(string Key, string Label)> Categories()
    {
        var list = modSystem.Emotes.Values
            .Where(e => !modSystem.IsEmoteDisabled(e.Code))
            .GroupBy(e => string.IsNullOrEmpty(e.Category) ? MiscKey : e.Category)
            .Select(g => (Key: g.Key, Label: modSystem.GetCategoryName(g.Key), Order: g.Min(e => e.CategoryOrder)))
            .OrderBy(c => c.Key == MiscKey ? 1 : 0)
            .ThenBy(c => c.Order)
            .ThenBy(c => c.Label, StringComparer.CurrentCultureIgnoreCase)
            .Select(c => (c.Key, c.Label))
            .ToList();

        list.Add((VanillaKey, Lang.Get("emotes:cat-vanilla")));
        return list;
    }

    public List<(string Code, string Name)> Entries(string key)
    {
        if (key == FavoritesKey)
        {
            var vanilla = VanillaCodes();
            return favorites.Codes
                .Where(c => IsAvailableModded(c) || vanilla.Contains(c, StringComparer.OrdinalIgnoreCase))
                .Select(c => (Code: c, Name: NameOf(c)))
                .ToList();
        }

        if (key == VanillaKey)
        {
            return VanillaCodes().Select(c => (Code: c, Name: modSystem.GetEmoteName(c))).ToList();
        }

        return modSystem.Emotes.Values
            .Where(e => (key == AllKey || e.Category == key) && !modSystem.IsEmoteDisabled(e.Code))
            .Select(e => (e.Code, Name: modSystem.GetEmoteName(e)))
            .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public string[] VanillaCodes()
    {
        var attr = capi.World?.Player?.Entity?.Properties?.Attributes?["emotes"];
        var codes = attr?.AsArray<string>()?.Where(c => !string.IsNullOrEmpty(c)).ToArray();
        return codes is { Length: > 0 } ? codes : VanillaFallback;
    }

    private bool IsAvailableModded(string code)
    {
        return modSystem.Emotes.ContainsKey(code) && !modSystem.IsEmoteDisabled(code);
    }

    private string NameOf(string code)
    {
        return modSystem.Emotes.TryGetValue(code, out var emote)
            ? modSystem.GetEmoteName(emote)
            : modSystem.GetEmoteName(code);
    }
}
