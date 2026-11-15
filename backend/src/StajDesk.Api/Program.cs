using Microsoft.AspNetCore.DataProtection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;
using StajDesk.Api.Common;
using StajDesk.Api.Features;
using StajDesk.Infrastructure;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;
using StajDesk.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);
// Yerel gizli değerler (depoya girmez; scripts/setup-secrets.mjs üretir). Ortam değişkenleri yine önceliklidir.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
var config = builder.Configuration;
SecretGuard.EnsureConfigured(config, "ConnectionStrings:Postgres", "RabbitMq:Password", "Jwt:Key", "Seed:DefaultPassword");

// ---------- Loglama: Console + Seq ----------
// Parola, token ve kişisel veri loglanmaz; yalnızca kimlikler (UserId) yazılır.
builder.Host.UseSerilog((ctx, lc) => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
    // Boş veritabanında EF Core, migration tablosunu sorgularken beklenen bir "tablo yok" hatası loglar; gerçek hata değildir
    .Filter.ByExcluding(e => e.Properties.TryGetValue("commandText", out var cmd) && cmd.ToString().Contains("__EFMigrationsHistory"))
    // Data Protection anahtar şifreleyicisi uyarısı Linux konteynerde beklenen durumdur (aşağıdaki nota bakın)
    .Filter.ByExcluding(e => e.MessageTemplate.Text.StartsWith("No XML encryptor configured"))
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "StajDesk.Api")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

// ---------- Data Protection anahtarları ----------
// Konteyner yeniden oluşturulduğunda anahtarlar kaybolmasın diye kalıcı birime yazılır (DataProtection:KeysPath).
// Anahtarlar dışarı açılmayan bir birimde durur; Linux'ta DPAPI olmadığından ayrıca şifrelenmez.
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
    builder.Services.AddDataProtection().SetApplicationName("StajDesk").PersistKeysToFileSystem(new DirectoryInfo(keysPath));

// ---------- Altyapı (EF Core, outbox, RabbitMQ) ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddStajDeskInfrastructure(config);

// ---------- Redis ----------
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var o = ConfigurationOptions.Parse(config.GetConnectionString("Redis") ?? "localhost:6379");
    o.AbortOnConnectFail = false; // Redis kapalıyken de API açılabilsin
    o.ConnectTimeout = 2000; o.SyncTimeout = 2000; o.AsyncTimeout = 2000;
    o.AllowAdmin = true;          // önbellek anahtarlarını listeleme (Cache Clear uygulaması)
    return ConnectionMultiplexer.Connect(o);
});
builder.Services.AddSingleton<ICacheService, RedisCacheService>();
builder.Services.AddSingleton<LoginRateLimiter>();

// ---------- Kimlik doğrulama ve yetkilendirme ----------
builder.Services.Configure<JwtOptions>(config.GetSection("Jwt"));
var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32) throw new InvalidOperationException("Jwt:Key en az 32 karakter olmalıdır (ortam değişkeni Jwt__Key).");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        NameClaimType = "name", RoleClaimType = "role", ClockSkew = TimeSpan.FromSeconds(30),
    };
    o.Events = new JwtBearerEvents
    {
        // WebSocket bağlantısı başlık taşıyamadığı için SignalR token'ı yalnızca hub yolunda sorgu dizesinden okunur.
        OnMessageReceived = ctx =>
        {
            var token = ctx.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs")) ctx.Token = token;
            return Task.CompletedTask;
        },
        OnChallenge = async ctx =>
        {
            ctx.HandleResponse();
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(ApiResponse.Fail("Oturumunuz sona erdi. Lütfen tekrar giriş yapınız.", code: "UNAUTHORIZED"));
        },
        OnForbidden = ctx =>
        {
            ctx.Response.StatusCode = 403;
            return ctx.Response.WriteAsJsonAsync(ApiResponse.Fail("Bu işlem için yetkiniz bulunmamaktadır.", code: "FORBIDDEN"));
        },
    };
});
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddSingleton<JwtTokenService>();

// ---------- Uygulama servisleri ----------
builder.Services.Configure<FileStorageOptions>(config.GetSection("Storage"));
builder.Services.AddScoped<FileStorage>();
builder.Services.AddScoped<AccessScope>();
builder.Services.Configure<LoginCodeOptions>(config.GetSection("Auth:LoginCode"));
builder.Services.AddScoped<LoginCodeService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<OrganizationService>();
builder.Services.AddScoped<AccountRequestService>();
builder.Services.AddScoped<DirectoryService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<HelpdeskConfigService>();
builder.Services.AddScoped<KnowledgeService>();
builder.Services.AddScoped<InventoryService>();
// Form türü işleyicileri (strateji deseni): yeni tür = yeni IFormKindHandler kaydı, motor değişmez.
builder.Services.AddScoped<LeaveService>();
builder.Services.AddScoped<SpendingLimitService>();
builder.Services.AddScoped<RevenueService>();
builder.Services.AddScoped<ShiftService>();
builder.Services.AddScoped<IFormKindHandler>(sp => sp.GetRequiredService<LeaveService>());
builder.Services.AddScoped<IFormKindHandler>(sp => sp.GetRequiredService<SpendingLimitService>());
builder.Services.AddScoped<IFormKindHandler>(sp => sp.GetRequiredService<RevenueService>());
builder.Services.AddScoped<IFormKindHandler>(sp => sp.GetRequiredService<ShiftService>());
builder.Services.AddScoped<FormKindRegistry>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<CollaborationService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<PublicSiteService>();
builder.Services.AddScoped<SystemService>();
builder.Services.AddScoped<AppCatalogService>();
builder.Services.AddHostedService<OutboxPublisherService>();

// ---------- Gerçek zamanlı bildirim (SignalR + Redis pub/sub) ----------
builder.Services.AddSignalR();
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, SubClaimUserIdProvider>();
builder.Services.AddHostedService<RealtimeRelayService>();
builder.Services.AddHttpClient("external", c =>
{
    c.Timeout = TimeSpan.FromSeconds(15); // TCMB gibi kamu kaynakları yoğun saatlerde 5-10 sn yanıt verebiliyor; sonuç önbelleğe alınır
    c.DefaultRequestHeaders.UserAgent.ParseAdd("StajDesk/1.0");
});
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddControllers(o => o.Filters.Add<ValidationFilter>())
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    })
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ctx =>
    {
        var errors = ctx.ModelState.Where(kv => kv.Value?.Errors.Count > 0)
            .ToDictionary(kv => JsonNamingPolicy.CamelCase.ConvertName(kv.Key.Split('.').Last().TrimStart('$')),
                kv => kv.Value!.Errors.Select(_ => "Geçersiz değer veya biçim.").Distinct().ToArray());
        return new BadRequestObjectResult(ApiResponse.Fail("Gönderilen bilgilerde hata var. Lütfen alanları kontrol ediniz.", errors));
    });

// ---------- Hız sınırlama (genel site uçları) ----------
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("public", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
});

// ---------- OpenAPI / Swagger ----------
builder.Services.AddOpenApi(o => o.AddDocumentTransformer((doc, _, _) =>
{
    doc.Info = new OpenApiInfo
    {
        Title = "StajDesk API", Version = "v1",
        Description = "Devoplus kurumsal çalışan portalı ve talep yönetim sistemi. Tüm yanıtlar { success, data, message, errors } zarfı ile döner. " +
                      "Korumalı uçlar için /api/auth/login ile alınan accessToken'ı 'Authorize' düğmesine giriniz.",
    };
    doc.Components ??= new OpenApiComponents();
    doc.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    doc.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", Description = "JWT erişim token'ı" };
    doc.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", doc)] = [] }];
    return Task.CompletedTask;
}));

// ---------- Sağlık kontrolleri ----------
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("postgres", tags: ["ready"])
    .AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(config.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseSerilogRequestLogging(o => o.GetLevel = (ctx, _, ex) =>
    ex is not null || ctx.Response.StatusCode >= 500 ? LogEventLevel.Error
    : ctx.Request.Path.StartsWithSegments("/health") || ctx.Request.Path.StartsWithSegments("/api/notifications") ? LogEventLevel.Debug
    : LogEventLevel.Information);
app.UseMiddleware<ExceptionMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<PasswordChangeGateMiddleware>();
app.UseAuthorization();

app.MapOpenApi();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/openapi/v1.json", "StajDesk API v1");
    o.RoutePrefix = "swagger";
    o.DocumentTitle = "StajDesk API";
});
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

static Task WriteHealth(HttpContext ctx, HealthReport report) => ctx.Response.WriteAsJsonAsync(new
{
    status = report.Status.ToString(),
    checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString(), e.Value.Description, ms = e.Value.Duration.TotalMilliseconds }),
});
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteHealth });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready"), ResponseWriter = WriteHealth });
app.MapGet("/api/health", () => Results.Ok(ApiResponse.Ok(new { status = "OK", serverTime = DateTime.UtcNow }))).AllowAnonymous();

// ---------- Migration ve başlangıç verisi ----------
if (!config.GetValue<bool>("SkipMigrations"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    for (var attempt = 1; ; attempt++)
    {
        try { await db.Database.MigrateAsync(); break; }
        catch (Exception ex) when (attempt < 15)
        {
            app.Logger.LogWarning("Veritabanına bağlanılamadı (deneme {Attempt}/15): {Error}", attempt, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
    var storage = scope.ServiceProvider.GetRequiredService<FileStorage>();
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync(new SeedOptions(
        config["Seed:DefaultPassword"] ?? throw new InvalidOperationException("Seed:DefaultPassword tanımlı değil (ortam değişkeni Seed__DefaultPassword)."),
        storage.Root,
        config["Seed:OrganizationFile"]));
}

app.Run();

public partial class Program;
