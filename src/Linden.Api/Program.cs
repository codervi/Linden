using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Linden.Api.Data;
using Linden.Api.Security;
using Linden.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- services ----
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<EncryptionOptions>(builder.Configuration.GetSection("Encryption"));
builder.Services.AddSingleton<INoteCipher, AesGcmNoteCipher>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();

builder.Services.AddIdentity<IdentityUser, IdentityRole>(opt =>
{
    opt.Password.RequiredLength = 12;
    opt.User.RequireUniqueEmail = true;
    opt.Lockout.MaxFailedAccessAttempts = 5;
    opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<AppDbContext>();

builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.Cookie.Name = "linden.auth";
    opt.Cookie.HttpOnly = true;
    opt.Cookie.SameSite = SameSiteMode.Strict;
    opt.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    opt.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    opt.SlidingExpiration = true;

    // This is an API: answer with a status code, never a redirect to a login page.
    opt.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    opt.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});

builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opt.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Fail at startup, not on the first request, if the encryption key is missing or wrong.
app.Services.GetRequiredService<INoteCipher>();

// ---- pipeline ----
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "no-referrer";

    // Swagger UI needs inline scripts, so the strict policy is not used in development.
    if (!app.Environment.IsDevelopment())
        h["Content-Security-Policy"] =
            "default-src 'self'; img-src 'self' data:; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";

    // Client data must not end up in a browser or proxy cache.
    if (ctx.Request.Path.StartsWithSegments("/api"))
    {
        h["Cache-Control"] = "no-store";
        h["Pragma"] = "no-cache";
    }
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();

// CSRF guard, on top of the SameSite=Strict cookie.
// A request that changes data must either send JSON or carry a custom header.
// A page on another site can do neither without a CORS preflight, and we allow no CORS.
app.Use(async (ctx, next) =>
{
    var m = ctx.Request.Method;
    var changesData = !(HttpMethods.IsGet(m) || HttpMethods.IsHead(m) || HttpMethods.IsOptions(m));

    if (changesData && ctx.Request.Path.StartsWithSegments("/api"))
    {
        var isJson = ctx.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ?? false;
        if (!isJson && !ctx.Request.Headers.ContainsKey("X-Requested-With"))
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 400,
                Title = "Request rejected",
                Detail = "Send JSON or the X-Requested-With header."
            });
            return;
        }
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await SeedData.RunAsync(app.Services, app.Configuration, app.Environment.IsDevelopment());

app.Run();
