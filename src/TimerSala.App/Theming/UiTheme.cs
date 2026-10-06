using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using TimerSala.Core.Storage;

namespace TimerSala.App.Theming;

/// <summary>
/// Tema (scuro, chiaro o come Windows) e scala dell'interfaccia di controller, mini, editor e impostazioni.
/// I colori sono pennelli condivisi delle risorse dell'applicazione: si cambiano al volo, senza riaprire le finestre.
/// Lo schermo della sala ha un tema suo (Impostazioni → Schermo) e non viene toccato.
/// </summary>
public static class UiTheme
{
    // chiave → (scuro, chiaro)
    static readonly Dictionary<string, (string Dark, string Light)> Colors = new()
    {
        ["BgBrush"] = ("#0F1115", "#F3F4F6"),
        ["CardBrush"] = ("#191C22", "#FFFFFF"),
        ["CardHoverBrush"] = ("#22262E", "#E8EBEF"),
        ["LineBrush"] = ("#2A2F38", "#D5DAE1"),
        ["TextBrush"] = ("#EEF0F3", "#111827"),
        ["MutedBrush"] = ("#8D94A0", "#5B6472"),
        ["AccentBrush"] = ("#3B82F6", "#2563EB"),
        ["GreenBrush"] = ("#22C55E", "#15803D"),
        ["AmberBrush"] = ("#FBBF24", "#B45309"),
        ["RedBrush"] = ("#EF4444", "#DC2626"),
        ["InputBrush"] = ("#111318", "#FFFFFF"),
        ["HoverBorderBrush"] = ("#3A404B", "#AEB6C2"),
        ["SelectionBrush"] = ("#2A3B5C", "#DBE7FE"),
        ["SubtleBrush"] = ("#14FFFFFF", "#0F000000"),
        ["SoftTextBrush"] = ("#C9CED6", "#374151"),
        ["DimTextBrush"] = ("#6B7280", "#6B7280"),
        ["FocusRingBrush"] = ("#93C5FD", "#2563EB"),
        ["OvertimeBgBrush"] = ("#1A0B0D", "#FDECEC"),
        ["PanelAltBrush"] = ("#1D2027", "#EEF0F3"),
    };

    static bool _prepared;

    public static bool IsLight { get; private set; }

    public static double Scale { get; private set; } = 1;

    /// <summary>Cambiato il tema o la scala: le finestre aperte si aggiornano.</summary>
    public static event EventHandler? Changed;

    static void Prepare()
    {
        if (_prepared) return;
        _prepared = true;
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && _mode == ControllerTheme.Auto)
                Application.Current.Dispatcher.BeginInvoke(() => Apply(_mode, Scale));
        };
    }

    static ControllerTheme _mode = ControllerTheme.Dark;

    public static void Apply(AppSettings s) => Apply(s.ControllerTheme, s.UiScalePercent / 100.0);

    public static void Apply(ControllerTheme mode, double scale)
    {
        Prepare();
        _mode = mode;
        bool light = mode switch
        {
            ControllerTheme.Light => true,
            ControllerTheme.Auto => WindowsUsesLightApps(),
            _ => false,
        };
        scale = Math.Clamp(scale, 0.8, 1.6);
        bool changed = light != IsLight || Math.Abs(scale - Scale) > 0.001;
        IsLight = light;
        var res = Application.Current.Resources;
        // i pennelli sono condivisi da tutti gli stili: cambiandone il colore si aggiorna tutto
        foreach (var (key, (dark, lightColor)) in Colors)
        {
            var color = Parse(light ? lightColor : dark);
            if (res[key] is SolidColorBrush { IsFrozen: false } b) b.Color = color;
            else res[key] = new SolidColorBrush(color);
        }
        Interop.DarkTitleBar.Light = light;

        double old = Scale;
        Scale = scale;
        if (!changed) return;
        foreach (Window w in Application.Current.Windows)
        {
            if (!Applies(w)) continue;
            Interop.DarkTitleBar.Apply(w, roundCorners: w.WindowStyle == WindowStyle.None);
            ApplyScale(w, old);
        }
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Lo schermo della sala e le finestre di servizio restano come sono.</summary>
    public static bool Applies(Window w) => w is not Views.TimerWindow && w.Content is FrameworkElement && w.AllowsTransparency == false;

    /// <summary>Alla prima apertura di una finestra: scala il contenuto e le dimensioni.</summary>
    public static void OnWindowLoaded(Window w)
    {
        if (!Applies(w)) return;
        ApplyScale(w, 1, firstTime: true);
    }

    static void ApplyScale(Window w, double old, bool firstTime = false)
    {
        if (w.Content is not FrameworkElement content) return;
        content.LayoutTransform = Math.Abs(Scale - 1) < 0.001 ? Transform.Identity : new ScaleTransform(Scale, Scale);
        if (w.SizeToContent == SizeToContent.WidthAndHeight) return;

        // le dimensioni minime crescono con la scala; quelle attuali in proporzione al cambio
        // (alla prima apertura solo se la finestra non ha una posizione ricordata, già a misura)
        double ratio = Scale / old;
        bool resize = !firstTime || w.WindowStartupLocation != WindowStartupLocation.Manual;
        var area = SystemParameters.WorkArea;
        if (w.SizeToContent != SizeToContent.Width)
        {
            if (w.MinWidth > 0) w.MinWidth = Math.Min(area.Width, w.MinWidth * ratio);
            if (resize && w.WindowState == WindowState.Normal && !double.IsNaN(w.Width)) w.Width = Math.Min(area.Width, w.Width * ratio);
        }
        if (w.SizeToContent != SizeToContent.Height)
        {
            if (w.MinHeight > 0) w.MinHeight = Math.Min(area.Height, w.MinHeight * ratio);
            if (resize && w.WindowState == WindowState.Normal && !double.IsNaN(w.Height)) w.Height = Math.Min(area.Height, w.Height * ratio);
        }
    }

    static bool WindowsUsesLightApps()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 1;
        }
        catch { return false; }
    }

    static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    /// <summary>Pennello del tema corrente (per i colori calcolati nel codice).</summary>
    public static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
