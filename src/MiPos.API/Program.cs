using MercadoPago.Config;
using MiPos.API.Hubs;
using MiPos.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuración de puerto para Render / Docker
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.Configure<Microsoft.AspNetCore.HostFiltering.HostFilteringOptions>(options =>
{
    options.AllowedHosts = new[] { "*" };
});

// Configuración de MercadoPago (Soporta appsettings.json y Variables de Entorno de Render)
string accessToken = builder.Configuration["MercadoPago:AccessToken"] 
    ?? builder.Configuration["MercadoPago__AccessToken"]
    ?? Environment.GetEnvironmentVariable("MercadoPago__AccessToken") 
    ?? Environment.GetEnvironmentVariable("MercadoPago:AccessToken")
    ?? "";

if (!string.IsNullOrEmpty(accessToken))
{
    MercadoPagoConfig.AccessToken = accessToken;
    string tokenInicio = accessToken.Substring(0, Math.Min(8, accessToken.Length));
    Console.WriteLine($"[INIT] MercadoPagoConfig configurado con exito. Token inicia en: {tokenInicio}");
}
else
{
    Console.WriteLine("[ERROR] No se encontro AccessToken de MercadoPago en la configuracion.");
}

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Redirigir la raiz (/) directamente a Swagger
app.MapGet("/", context =>
{
    context.Response.Redirect("/swagger/index.html");
    return Task.CompletedTask;
});

// Habilitar Swagger en TODOS los entornos (Development y Production)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MiPos API v1");
    c.RoutePrefix = "swagger";
});

app.UseAuthorization();
app.MapControllers();
app.MapHub<PagoHub>("/pagohub");

app.Run();