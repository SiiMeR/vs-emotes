using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Emotes;

public class EmoteCommands
{
    private const int ShowcaseStartDelayMs = 3000;
    private const int ShowcaseHoldMs = 4000;
    private const int ShowcaseGapMs = 1000;

    private readonly EmotesModSystem system;
    private readonly ICoreServerAPI api;
    private readonly EmoteServer server;

    public EmoteCommands(EmotesModSystem system, ICoreServerAPI api, EmoteServer server)
    {
        this.system = system;
        this.api = api;
        this.server = server;
    }

    public void Register()
    {
        var cmd = api.ChatCommands
            .GetOrCreate("emotes")
            .RequiresPrivilege(Privilege.chat)
            .WithDescription(Lang.Get("emotes:cmd-description"));

        cmd.BeginSubCommand("play")
            .RequiresPlayer()
            .WithArgs(api.ChatCommands.Parsers.Word("name"))
            .HandleWith(HandlePlay)
            .EndSubCommand();

        cmd.BeginSubCommand("list")
            .HandleWith(HandleList)
            .EndSubCommand();

        cmd.BeginSubCommand("stop")
            .RequiresPlayer()
            .HandleWith(HandleStop)
            .EndSubCommand();

        cmd.BeginSubCommand("showcase")
            .RequiresPlayer()
            .WithArgs(api.ChatCommands.Parsers.OptionalWord("category"))
            .HandleWith(HandleShowcase)
            .EndSubCommand();

        cmd.BeginSubCommand("consent")
            .RequiresPlayer()
            .WithArgs(api.ChatCommands.Parsers.Bool("autoaccept"))
            .HandleWith(HandleConsent)
            .EndSubCommand();

        cmd.BeginSubCommand("accept")
            .RequiresPlayer()
            .HandleWith(args => server.Pairs.Accept(args.Caller.Player as IServerPlayer))
            .EndSubCommand();

        cmd.BeginSubCommand("refuse")
            .RequiresPlayer()
            .HandleWith(args => server.Pairs.Refuse(args.Caller.Player as IServerPlayer))
            .EndSubCommand();
    }

    private TextCommandResult HandlePlay(TextCommandCallingArgs args)
    {
        if (args.Caller.Entity is not EntityPlayer player)
            return TextCommandResult.Error(Lang.Get("emotes:cmd-only-players"));

        if (EmoteState.InCarry(player)) return TextCommandResult.Error(Lang.Get("emotes:cmd-carrying"));

        if (player.MountedOn != null) return TextCommandResult.Error(Lang.Get("emotes:cmd-mounted"));

        var key = ((string)args[0]).ToLowerInvariant();
        if (!system.Emotes.TryGetValue(key, out var emote))
            return TextCommandResult.Error(Lang.Get("emotes:cmd-not-found", key));

        if (server.IsDisabled(key)) return TextCommandResult.Error(Lang.Get("emotes:cmd-disabled", key));

        if (emote.RequiresTarget) return server.Pairs.Initiate(key, args.Caller.Player as IServerPlayer);

        var started = EmoteState.Toggle(player, emote.Code);
        var displayName = system.GetEmoteName(emote);
        return TextCommandResult.Success(started
            ? Lang.Get("emotes:cmd-emote-started", displayName)
            : Lang.Get("emotes:cmd-emote-stopped", displayName));
    }

    private TextCommandResult HandleList(TextCommandCallingArgs args)
    {
        var available = system.Emotes.Values.Where(e => !server.IsDisabled(e.Code)).ToArray();

        var solo = Describe(available.Where(e => !e.RequiresTarget));
        var paired = Describe(available.Where(e => e.RequiresTarget));

        var message = Lang.Get("emotes:cmd-available-emotes", solo);
        if (!string.IsNullOrEmpty(paired)) message += "\n" + Lang.Get("emotes:cmd-paired-emotes", paired);

        return TextCommandResult.Success(message);
    }

    private string Describe(IEnumerable<CustomEmote> emotes)
    {
        return string.Join(", ", emotes.Select(e => $"{e.Code} ({system.GetEmoteName(e)})"));
    }

    private TextCommandResult HandleStop(TextCommandCallingArgs args)
    {
        if (args.Caller.Entity is not EntityPlayer player)
            return TextCommandResult.Error(Lang.Get("emotes:cmd-only-players"));

        EmoteState.StopAll(player);
        return TextCommandResult.Success(Lang.Get("emotes:cmd-all-stopped"));
    }

    private TextCommandResult HandleConsent(TextCommandCallingArgs args)
    {
        if (args.Caller.Player is not IServerPlayer player)
            return TextCommandResult.Error(Lang.Get("emotes:cmd-only-players"));

        var requireConsent = (bool)args[0];
        player.SetModData(EmotePairs.ConsentKey, requireConsent);
        return TextCommandResult.Success(Lang.Get(requireConsent ? "emotes:consent-on" : "emotes:consent-off"));
    }

    private TextCommandResult HandleShowcase(TextCommandCallingArgs args)
    {
        if (args.Caller.Entity is not EntityPlayer player)
            return TextCommandResult.Error(Lang.Get("emotes:cmd-only-players"));

        var category = (args[0] as string)?.Trim().ToLowerInvariant();

        var codes = system.Emotes.Values
            .Where(e => !server.IsDisabled(e.Code))
            .Where(e => string.IsNullOrEmpty(category) || e.Category == category)
            .Select(e => e.Code)
            .ToList();

        if (codes.Count == 0)
            return TextCommandResult.Error(Lang.Get("emotes:cmd-showcase-empty", Categories()));

        new Showcase(api, player, codes).Start();
        return TextCommandResult.Success(string.IsNullOrEmpty(category)
            ? Lang.Get("emotes:cmd-showcase-start")
            : Lang.Get("emotes:cmd-showcase-start-category", system.GetCategoryName(category)));
    }

    private string Categories()
    {
        return string.Join(", ", system.Emotes.Values
            .Where(e => !server.IsDisabled(e.Code))
            .Select(e => e.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase));
    }

    private class Showcase
    {
        private readonly ICoreServerAPI api;
        private readonly EntityPlayer player;
        private readonly List<string> codes;
        private long tickListenerId;
        private bool cancelled;

        public Showcase(ICoreServerAPI api, EntityPlayer player, List<string> codes)
        {
            this.api = api;
            this.player = player;
            this.codes = codes;
        }

        public void Start()
        {
            tickListenerId = api.Event.RegisterGameTickListener(_ => CheckCancelled(), 100);
            api.Event.RegisterCallback(_ => Run(0), ShowcaseStartDelayMs);
        }

        private void CheckCancelled()
        {
            if (cancelled)
            {
                Stop();
                return;
            }

            var controls = player.ServerControls;
            if (controls == null || (!controls.TriesToMove && !controls.Jump)) return;

            cancelled = true;
            Stop();
            EmoteState.StopAll(player);
        }

        private void Stop()
        {
            api.Event.UnregisterGameTickListener(tickListenerId);
        }

        private void Run(int index)
        {
            if (!player.Alive || cancelled) return;

            if (index >= codes.Count)
            {
                EmoteState.StopAll(player);
                Stop();
                return;
            }

            EmoteState.Play(player, codes[index]);
            api.Event.RegisterCallback(_ =>
            {
                if (!player.Alive || cancelled) return;

                EmoteState.StopAll(player);
                api.Event.RegisterCallback(_ => Run(index + 1), ShowcaseGapMs);
            }, ShowcaseHoldMs);
        }
    }
}
