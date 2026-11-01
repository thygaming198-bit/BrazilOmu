namespace BrazilOmu;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
        var saved = Preferences.Get("saved_key", "");
        if (!string.IsNullOrEmpty(saved))
        {
            KeyEntry.Text = saved;
            Loaded += async (_, _) => await TryLogin(saved, silent: true);
        }
    }

    async void OnEnterClicked(object? sender, EventArgs e) => await TryLogin(KeyEntry.Text ?? "", silent: false);

    async Task TryLogin(string key, bool silent)
    {
        ErrorLabel.IsVisible = false;
        EnterButton.IsEnabled = false;
        Spinner.IsVisible = Spinner.IsRunning = true;

        var res = await ApiService.LoginAsync(key);

        Spinner.IsVisible = Spinner.IsRunning = false;
        EnterButton.IsEnabled = true;

        if (res.Ok)
        {
            Preferences.Set("saved_key", key.Trim());
            await Navigation.PushAsync(new DashboardPage(res.Label, res.ExpiresAt));
        }
        else
        {
            if (!silent || res.Message.Contains("expired") || res.Message.Contains("suspended"))
                Preferences.Remove("saved_key");
            ErrorLabel.Text = res.Message;
            ErrorLabel.IsVisible = true;
        }
    }
}
