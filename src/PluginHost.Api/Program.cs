using PluginHost.Api.Middleware;
using PluginHost.Api.Services;
using PluginHost.Runtime;
using PluginHost.Security;
using static Microsoft.AspNetCore.Http.StatusCodes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var securityOptions = builder.Configuration
  .GetSection("Security").Get<SecurityOptions>() ?? new SecurityOptions();
builder.Services.AddSingleton(securityOptions);
builder.Services.AddSingleton<IEcdhKeyProvider, EcdhKeyProvider>();
builder.Services.AddSingleton<HandshakeService>();
builder.Services.AddSingleton<AesGcmEnvelopeCrypto>();
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddHostedService<SessionCleanupHostedService>();

var pluginStoragePath = Path.Combine(builder.Environment.ContentRootPath, "plugin_storage");
builder.Services.AddSingleton(new PluginManager(pluginStoragePath));

builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(60);
    options.ExcludedHosts.Add("example.com");
    options.ExcludedHosts.Add("www.example.com");
});

builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = Status307TemporaryRedirect;
    options.HttpsPort = builder.Configuration.GetValue<int>("https_port");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<EncryptionMiddleware>();

app.MapControllers();

app.Run();

public partial class Program
{
}
