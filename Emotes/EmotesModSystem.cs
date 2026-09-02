using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace Emotes;

public class EmotesModSystem : ModSystem
{
    internal const string ChannelName = "emotes";

    private readonly HashSet<string> warned = new();

    private EmoteClient client;
    private EmoteServer server;
    private EmoteNames names;

    private IReadOnlyDictionary<string, CustomEmote> emotes =
        new Dictionary<string, CustomEmote>(StringComparer.OrdinalIgnoreCase);

    private Animation[] injectedAnimations = Array.Empty<Animation>();

    public IReadOnlyDictionary<string, CustomEmote> Emotes => emotes;

    public Animation[] InjectedAnimations => injectedAnimations;

    public override void AssetsLoaded(ICoreAPI api)
    {
        base.AssetsLoaded(api);

        var result = EmoteLoader.Load(api, Mod.Logger);
        emotes = result.Emotes;
        injectedAnimations = result.InjectedAnimations;
        names = new EmoteNames(emotes);

        Mod.Logger.Notification("Loaded {0} emotes from {1} files. Skipped: {2}. Animations injected: {3}",
            result.Emotes.Count, result.FileCount, result.SkippedCount, result.InjectedAnimations.Length);
    }

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterEntityBehaviorClass("emotes", typeof(BehaviorEmotes));
        api.Event.OnEntitySpawn += OnEntitySpawn;
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);

        if (api.ModLoader.IsModEnabled("overhaullib")) CombatOverhaulPatch.Apply(api);

        client = new EmoteClient(this, api);
        names?.ReportMissing(Mod.Logger);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        server = new EmoteServer(this, api);
    }

    public override void Dispose()
    {
        if (client != null) CombatOverhaulPatch.Remove();
        base.Dispose();
    }

    public string GetEmoteName(CustomEmote emote)
    {
        return names?.OfEmote(emote) ?? "";
    }

    public string GetEmoteName(string code)
    {
        return names?.OfCode(code) ?? "";
    }

    public string GetCategoryName(string category)
    {
        return names?.OfCategory(category) ?? "";
    }

    public bool IsEmoteDisabled(string code)
    {
        return client?.IsDisabled(code) ?? false;
    }

    public void SendToggleEmote(string code)
    {
        client?.SendToggle(code);
    }

    public void SendStopEmotes()
    {
        client?.SendStop();
    }

    public void TryEndPair(EntityPlayer player)
    {
        server?.Pairs.End(player);
    }

    public bool MarkWarned(string key)
    {
        lock (warned)
        {
            return warned.Add(key);
        }
    }

    public bool MarkShapeValidated(string shapePath)
    {
        return MarkWarned("shape/" + shapePath);
    }

    internal void FilterInjectedAnimations(HashSet<string> disabledCodes)
    {
        injectedAnimations = emotes.Values
            .Where(e => e.InjectedAnimation != null && !disabledCodes.Contains(e.Code))
            .Select(e => e.InjectedAnimation)
            .ToArray();
    }

    private void OnEntitySpawn(Entity entity)
    {
        if (entity is not EntityPlayer) return;
        if (entity.GetBehavior<BehaviorEmotes>() != null) return;

        var behavior = new BehaviorEmotes(entity);
        entity.AddBehavior(behavior);
        behavior.Initialize(entity.Properties, null);
    }
}
