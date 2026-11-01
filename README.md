# Brazil Omu

Key-protected mobile app (.NET MAUI, iOS) + license server (ASP.NET Core + SQL Server) with an admin page to generate keys.

```
BrazilOmu/
  App/      .NET MAUI iOS app (English UI)
  Server/   ASP.NET Core 8 API + admin page (/admin)
  .github/workflows/build-ios.yml   builds an unsigned .ipa on GitHub's Mac runner
```

## 1. Run the server (Windows)

1. Install .NET 8 SDK and SQL Server (or SQL Express).
2. Edit `Server/appsettings.json`: set the SQL connection string and **change `Admin:Token`** (your admin password).
3. Run:
   ```
   cd Server
   dotnet run
   ```
4. Open `http://localhost:5000/admin`, sign in with the admin password, and generate keys
   (label, valid days, max devices, quantity). You can suspend, extend, reset devices, or delete keys.
5. Deploy it to a host with a public **HTTPS** domain (iOS blocks plain HTTP). Any Windows VPS, Azure App Service, or similar works.

## 2. Point the app at your server

Edit `App/ApiService.cs`:
```csharp
public const string ApiBaseUrl = "https://YOUR-SERVER-DOMAIN.com";
```

## 3. Build the .ipa with GitHub Actions

1. Create a GitHub repo and upload the whole `BrazilOmu` folder contents (the `.github` folder must be at the repo root).
2. Open the **Actions** tab, choose **Build iOS IPA (unsigned)**, click **Run workflow**.
3. When it finishes, download `BrazilOmu-ipa` under *Artifacts* and unzip to get `BrazilOmu.ipa`.

## 4. Install with E-Sign

Send the `.ipa` to your iPhone, open it in E-Sign, sign it with your certificate, and install.

## Notes

- Unsigned MAUI iOS builds can occasionally fail on a particular Xcode/workload version. If the build step errors, send me the log and I'll adjust the workflow.
- Free/personal certificates expire after 7 days and need re-signing.
- App flow: Login (key) -> server checks key, expiry, suspension and device limit -> Dashboard (settings toggles, apply button, activity log, ONLINE status).
