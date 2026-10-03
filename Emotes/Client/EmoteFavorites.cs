using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;

namespace Emotes;

public class EmoteFavorites
{
    public const string Prefix = "* ";
    private const string FileName = "emotes-client.json";

    private readonly ICoreClientAPI capi;
    private readonly EmotesClientConfig config;
    private readonly HashSet<string> set = new(StringComparer.OrdinalIgnoreCase);

    public EmoteFavorites(ICoreClientAPI capi)
    {
        this.capi = capi;
        config = Load();
        config.Favorites = (config.Favorites ?? new List<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c) && set.Add(c))
            .ToList();
        Save();
    }

    public IReadOnlyList<string> Codes => config.Favorites;

    public bool Contains(string code)
    {
        return code != null && set.Contains(code);
    }

    public bool Toggle(string code)
    {
        if (string.IsNullOrEmpty(code)) return false;

        if (set.Remove(code))
        {
            config.Favorites.RemoveAll(c => string.Equals(c, code, StringComparison.OrdinalIgnoreCase));
            Save();
            return false;
        }

        set.Add(code);
        config.Favorites.Add(code);
        Save();
        return true;
    }

    public string Decorate(string code, string name)
    {
        return Contains(code) ? Prefix + name : name;
    }

    private EmotesClientConfig Load()
    {
        try
        {
            return capi.LoadModConfig<EmotesClientConfig>(FileName) ?? new EmotesClientConfig();
        }
        catch (Exception)
        {
            return new EmotesClientConfig();
        }
    }

    private void Save()
    {
        capi.StoreModConfig(config, FileName);
    }
}
