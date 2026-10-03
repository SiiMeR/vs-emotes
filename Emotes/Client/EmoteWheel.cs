using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace Emotes;

public class EmoteWheel : IRenderer
{
    public const string HotkeyCode = "emotewheel";

    private const int InnerRadius = 110;
    private const int OuterRadius = 250;
    private const int MaxSlices = 16;

    private readonly ICoreClientAPI capi;
    private readonly EmoteClient client;
    private readonly EmoteCatalog catalog;
    private readonly EmoteFavorites favorites;
    private readonly EmoteRadialMenu menu;
    private List<(string Key, string Label)> categories = new();
    private string categoryKey = EmoteCatalog.FavoritesKey;
    private Entity target;
    private bool held;
    private bool sawHotkeyDown;
    private bool disposed;

    public bool Opened => menu.Opened;

    public double RenderOrder => 1.5;

    public int RenderRange => 1;

    public EmoteWheel(ICoreClientAPI capi, EmoteClient client, EmoteCatalog catalog, EmoteFavorites favorites)
    {
        this.capi = capi;
        this.client = client;
        this.catalog = catalog;
        this.favorites = favorites;
        menu = new EmoteRadialMenu(capi, InnerRadius, OuterRadius);

        capi.Input.RegisterHotKey(HotkeyCode, Lang.Get("emotes:hotkey-wheel"), GlKeys.K, HotkeyType.CharacterControls);
        capi.Input.SetHotKeyHandler(HotkeyCode, OnHotkey);
        capi.Event.RegisterRenderer(this, EnumRenderStage.Ortho, HotkeyCode);
        capi.Event.MouseMove += OnMouseMove;
        capi.Event.MouseDown += OnMouseDown;
        capi.Event.MouseUp += OnMouseUp;
        capi.Event.KeyUp += OnKeyUp;
    }

    private bool OnHotkey(KeyCombination combination)
    {
        if (disposed || held) return menu.Opened;
        if (!TryOpen()) return false;

        held = true;
        sawHotkeyDown = false;
        return true;
    }

    private bool TryOpen()
    {
        var self = capi.World?.Player?.Entity;
        if (self == null || !self.Alive || client.DialogOpen) return false;

        target = capi.World.Player.CurrentEntitySelection?.Entity;
        categories = new List<(string Key, string Label)> { (EmoteCatalog.FavoritesKey, Lang.Get("emotes:cat-favorites")) };
        categories.AddRange(catalog.Categories());
        if (categories.All(c => c.Key != categoryKey)) categoryKey = EmoteCatalog.FavoritesKey;

        menu.Open();
        Rebuild();
        return menu.Opened;
    }

    private void Rebuild()
    {
        var label = categories.FirstOrDefault(c => c.Key == categoryKey).Label ?? "";
        var entries = catalog.Entries(categoryKey)
            .Take(MaxSlices)
            .Select(e => new EmoteMenuEntry(e.Code, e.Name, favorites.Contains(e.Code)))
            .ToList();

        menu.Build(entries, label, Lang.Get("emotes:favorites-empty"));
        menu.Point(capi.Input.MouseX, capi.Input.MouseY);
    }

    private int? MappedKeyCode()
    {
        return capi.Input.HotKeys.TryGetValue(HotkeyCode, out var hotkey) ? hotkey?.CurrentMapping?.KeyCode : null;
    }

    private void OnKeyUp(KeyEvent e)
    {
        if (MappedKeyCode() == e.KeyCode) Commit();
    }

    private void OnMouseUp(MouseEvent e)
    {
        if (MappedKeyCode() == KeyCombination.MouseStart + (int)e.Button) Commit();
    }

    private void OnMouseMove(MouseEvent e)
    {
        if (menu.Opened) menu.Point(e.X, e.Y);
    }

    public void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (disposed || !menu.Opened) return;

        e.SetHandled();
        if (e.delta == 0 || categories.Count == 0) return;

        var index = categories.FindIndex(c => c.Key == categoryKey);
        var next = (index + (e.delta > 0 ? -1 : 1) + categories.Count) % categories.Count;
        categoryKey = categories[next].Key;
        Rebuild();
    }

    private void OnMouseDown(MouseEvent e)
    {
        if (!menu.Opened) return;

        e.Handled = true;
        if (MappedKeyCode() == KeyCombination.MouseStart + (int)e.Button) return;

        if (e.Button == EnumMouseButton.Right)
        {
            ToggleHovered();
            return;
        }

        if (e.Button != EnumMouseButton.Left) return;

        menu.Point(e.X, e.Y);
        Play(menu.Close());
    }

    private void ToggleHovered()
    {
        var code = menu.HoveredCode;
        if (code == null) return;

        favorites.Toggle(code);
        Rebuild();
    }

    private bool HotkeyDown()
    {
        var state = capi.Input.KeyboardKeyState;
        var code = MappedKeyCode() ?? -1;
        return code >= 0 && state != null && code < state.Length && state[code];
    }

    private void Commit()
    {
        if (!held) return;

        held = false;
        Play(menu.Close());
    }

    private void Play(string code)
    {
        if (code == null) return;
        client.Play(code, target);
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (held && HotkeyDown())
        {
            sawHotkeyDown = true;
        }
        else if (held && sawHotkeyDown)
        {
            Commit();
            return;
        }

        if (!menu.Opened) return;

        var shader = capi.Render.CurrentActiveShader == null
            ? capi.Render.GetEngineShader(EnumShaderProgram.Gui)
            : null;

        shader?.Use();
        capi.Render.GLDisableDepthTest();
        menu.Render();
        capi.Render.GLEnableDepthTest();
        shader?.Stop();
    }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;
        capi.Event.UnregisterRenderer(this, EnumRenderStage.Ortho);
        capi.Event.MouseMove -= OnMouseMove;
        capi.Event.MouseDown -= OnMouseDown;
        capi.Event.MouseUp -= OnMouseUp;
        capi.Event.KeyUp -= OnKeyUp;
        menu.Dispose();
    }
}
