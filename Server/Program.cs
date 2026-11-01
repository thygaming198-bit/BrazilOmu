using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDb>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDb>().Database.EnsureCreated();
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

var adminToken = app.Configuration["Admin:Token"] ?? "CHANGE-ME";

bool IsAdmin(HttpRequest r) =>
    r.Headers.TryGetValue("X-Admin-Token", out var t) && t.ToString() == adminToken;

static string NewKey()
{
    const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    string Part() => new(Enumerable.Range(0, 4).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray());
    return $"BO-{Part()}-{Part()}-{Part()}";
}

app.MapGet("/admin", () => Results.Redirect("/admin.html"));
app.MapGet("/api/health", () => Results.Ok(new { status = "online", time = DateTime.UtcNow }));

// ---------- Client login ----------
app.MapPost("/api/auth/login", async (LoginRequest req, AppDb db) =>
{
    var key = (req.Key ?? "").Trim().ToUpperInvariant();
    var device = (req.DeviceId ?? "").Trim();
    if (key.Length == 0 || device.Length == 0)
        return Results.Ok(new LoginResponse(false, "Please enter your access key.", null, null));

    var lk = await db.Keys.Include(k => k.Activations).FirstOrDefaultAsync(k => k.Value == key);
    if (lk is null) return Results.Ok(new LoginResponse(false, "Invalid access key.", null, null));
    if (!lk.IsActive) return Results.Ok(new LoginResponse(false, "This key has been suspended.", null, null));
    if (lk.ExpiresAt < DateTime.UtcNow) return Results.Ok(new LoginResponse(false, "This key has expired.", null, null));

    var act = lk.Activations.FirstOrDefault(a => a.DeviceId == device);
    if (act is null)
    {
        if (lk.Activations.Count >= lk.MaxDevices)
            return Results.Ok(new LoginResponse(false, "Device limit reached for this key.", null, null));
        lk.Activations.Add(new Activation { DeviceId = device, FirstSeen = DateTime.UtcNow, LastSeen = DateTime.UtcNow });
    }
    else act.LastSeen = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(new LoginResponse(true, "Welcome to Brazil Omu.", lk.ExpiresAt, lk.Label));
});

// ---------- Admin API ----------
app.MapGet("/api/admin/keys", async (HttpRequest r, AppDb db) =>
{
    if (!IsAdmin(r)) return Results.Unauthorized();
    var list = await db.Keys.Include(k => k.Activations).OrderByDescending(k => k.CreatedAt)
        .Select(k => new
        {
            k.Id, key = k.Value, k.Label, k.ExpiresAt, k.MaxDevices, k.IsActive, k.CreatedAt,
            devices = k.Activations.Count
        }).ToListAsync();
    return Results.Ok(list);
});

app.MapPost("/api/admin/keys", async (HttpRequest r, CreateKeyRequest req, AppDb db) =>
{
    if (!IsAdmin(r)) return Results.Unauthorized();
    var count = Math.Clamp(req.Count, 1, 100);
    var days = Math.Clamp(req.Days, 1, 3650);
    var devices = Math.Clamp(req.MaxDevices, 1, 50);
    var created = new List<string>();
    for (int i = 0; i < count; i++)
    {
        string k;
        do { k = NewKey(); } while (await db.Keys.AnyAsync(x => x.Value == k));
        db.Keys.Add(new LicenseKey
        {
            Value = k, Label = req.Label ?? "", MaxDevices = devices,
            ExpiresAt = DateTime.UtcNow.AddDays(days), IsActive = true, CreatedAt = DateTime.UtcNow
        });
        created.Add(k);
    }
    await db.SaveChangesAsync();
    return Results.Ok(created);
});

app.MapPost("/api/admin/keys/{id:int}/toggle", async (HttpRequest r, int id, AppDb db) =>
{
    if (!IsAdmin(r)) return Results.Unauthorized();
    var k = await db.Keys.FindAsync(id);
    if (k is null) return Results.NotFound();
    k.IsActive = !k.IsActive;
    await db.SaveChangesAsync();
    return Results.Ok(new { k.Id, k.IsActive });
});

app.MapPost("/api/admin/keys/{id:int}/extend/{days:int}", async (HttpRequest r, int id, int days, AppDb db) =>
{
    if (!IsAdmin(r)) return Results.Unauthorized();
    var k = await db.Keys.FindAsync(id);
    if (k is null) return Results.NotFound();
    var baseDate = k.ExpiresAt > DateTime.UtcNow ? k.ExpiresAt : DateTime.UtcNow;
    k.ExpiresAt = baseDate.AddDays(Math.Clamp(days, 1, 3650));
    await db.SaveChangesAsync();
    return Results.Ok(new { k.Id, k.ExpiresAt });
});

app.MapPost("/api/admin/keys/{id:int}/reset-devices", async (HttpRequest r, int id, AppDb db) =>
{
    if (!IsAdmin(r)) return Results.Unauthorized();
    var acts = db.Activations.Where(a => a.LicenseKeyId == id);
    db.Activations.RemoveRange(acts);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapDelete("/api/admin/keys/{id:int}", async (HttpRequest r, int id, AppDb db) =>
{
    if (!IsAdmin(r)) return Results.Unauthorized();
    var k = await db.Keys.FindAsync(id);
    if (k is null) return Results.NotFound();
    db.Keys.Remove(k);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.Run();

// ---------- Models ----------
public record LoginRequest(string? Key, string? DeviceId);
public record LoginResponse(bool Ok, string Message, DateTime? ExpiresAt, string? Label);
public record CreateKeyRequest(string? Label, int Days, int MaxDevices, int Count);

public class LicenseKey
{
    public int Id { get; set; }
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public int MaxDevices { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public List<Activation> Activations { get; set; } = new();
}

public class Activation
{
    public int Id { get; set; }
    public int LicenseKeyId { get; set; }
    public string DeviceId { get; set; } = "";
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
}

public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> o) : base(o) { }
    public DbSet<LicenseKey> Keys => Set<LicenseKey>();
    public DbSet<Activation> Activations => Set<Activation>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<LicenseKey>().HasIndex(k => k.Value).IsUnique();
        b.Entity<LicenseKey>().HasMany(k => k.Activations).WithOne().HasForeignKey(a => a.LicenseKeyId).OnDelete(DeleteBehavior.Cascade);
    }
}
