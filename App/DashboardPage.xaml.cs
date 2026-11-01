using System.Text;

namespace BrazilOmu;

public partial class DashboardPage : ContentPage
{
    bool _loading = true;

    public DashboardPage(string? label, DateTime? expiresAt)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(label)) WelcomeLabel.Text = $"Welcome, {label}";
        if (expiresAt is not null) ExpiryLabel.Text = $"Key valid until {expiresAt.Value.ToLocalTime():yyyy-MM-dd}";

        SwNotif.IsToggled = Preferences.Get("notif", false);
        SwHaptic.IsToggled = Preferences.Get("haptic", false);
        SwSync.IsToggled = Preferences.Get("sync", false);
        SwSaver.IsToggled = Preferences.Get("saver", false);
        SwSound.IsToggled = Preferences.Get("sound", false);
        _loading = false;

        Loaded += async (_, _) => await RefreshStatus();
    }

    async Task RefreshStatus()
    {
        var ok = await ApiService.PingAsync();
        StatusLabel.Text = ok ? "● ONLINE" : "● OFFLINE";
        StatusLabel.TextColor = ok ? Color.FromArgb("#2ECC71") : Color.FromArgb("#FF5C7A");
        StatusBadge.BackgroundColor = ok ? Color.FromArgb("#12351f") : Color.FromArgb("#3a1420");
    }

    int ActiveCount() => new[] { SwNotif, SwHaptic, SwSync, SwSaver, SwSound }.Count(s => s.IsToggled);

    void OnToggled(object? sender, ToggledEventArgs e)
    {
        if (_loading) return;
        if (SwHaptic.IsToggled)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
        }
    }

    void OnApplyClicked(object? sender, EventArgs e)
    {
        Preferences.Set("notif", SwNotif.IsToggled);
        Preferences.Set("haptic", SwHaptic.IsToggled);
        Preferences.Set("sync", SwSync.IsToggled);
        Preferences.Set("saver", SwSaver.IsToggled);
        Preferences.Set("sound", SwSound.IsToggled);

        var sb = new StringBuilder();
        sb.AppendLine("> [APP] Initializing panel...");
        sb.AppendLine("> [SYSTEM] Loading selected settings...");
        sb.AppendLine($"> [CONFIG] Active options: {ActiveCount()}");
        sb.AppendLine("> [STORAGE] Saving preferences...");
        sb.AppendLine("> [STATUS] Settings applied successfully.");
        LogLabel.Text = sb.ToString().TrimEnd();

        if (SwHaptic.IsToggled)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { }
        }
    }

    async void OnSignOutClicked(object? sender, EventArgs e)
    {
        Preferences.Remove("saved_key");
        await Navigation.PopAsync();
    }
}
