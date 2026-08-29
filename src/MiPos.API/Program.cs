using MercadoPago.Config;
using MiPos.API.Hubs;
using MiPos.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuración de puerto para Render / Docker
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"[http://0.0.0.0:{port}](http://0.0.0.0:{port})");

// Permitir todos los hosts
builder.Services.Configure<Microsoft.AspNetCore.HostFiltering.HostFilteringOptions>(options =>
{
options.AllowedHosts = new[] { "*" };
});

// Configuración de MercadoPago
// Soporta appsettings.json y variables de entorno de Render
string accessToken =
builder.Configuration["MercadoPago:AccessToken"]
?? builder.Configuration["MercadoPago__AccessToken"]
?? Environment.GetEnvironmentVariable("MercadoPago__AccessToken")
?? Environment.GetEnvironmentVariable("MercadoPago:AccessToken")
?? "";

if (!string.IsNullOrEmpty(accessToken))
{
MercadoPagoConfig.AccessToken = accessToken;

string tokenInicio = accessToken.Substring(
    0,
    Math.Min(8, accessToken.Length)
);

Console.WriteLine(
    $"[INIT] MercadoPagoConfig configurado con éxito. Token inicia en: {tokenInicio}"
);


}
else
{
Console.WriteLine(
"[ERROR] No se encontró AccessToken de MercadoPago en la configuración."
);
}

// Configuración SMTP
// Las variables de Render se leen automáticamente:
// Smtp__Host     -> Smtp:Host
// Smtp__Port     -> Smtp:Port
// Smtp__Username -> Smtp:Username
// Smtp__Password -> Smtp:Password

Console.WriteLine(
$"[INIT SMTP] Host configurado: {!string.IsNullOrEmpty(builder.Configuration["Smtp:Host"])}"
);

Console.WriteLine(
$"[INIT SMTP] Puerto: {builder.Configuration["Smtp:Port"]}"
);

Console.WriteLine(
$"[INIT SMTP] Usuario configurado: {!string.IsNullOrEmpty(builder.Configuration["Smtp:Username"])}"
);

Console.WriteLine(
$"[INIT SMTP] Password configurado: {!string.IsNullOrEmpty(builder.Configuration["Smtp:Password"])}"
);

// Configuración de CORS para SignalR y peticiones externas
builder.Services.AddCors(options =>
{
options.AddPolicy("AllowAll", policy =>
{
policy
.SetIsOriginAllowed(_ => true)
.AllowAnyMethod()
.AllowAnyHeader()
.AllowCredentials();
});
});

// Servicios
builder.Services.AddControllers();

builder.Services.AddSignalR();

builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

var app = builder.Build();

// CORS
app.UseCors("AllowAll");

// Redirigir la raíz (/) directamente a Swagger
app.MapGet("/", context =>
{
context.Response.Redirect("/swagger/index.html");
return Task.CompletedTask;
});

// Swagger habilitado en todos los entornos
app.UseSwagger();

app.UseSwaggerUI(c =>
{
c.SwaggerEndpoint(
"/swagger/v1/swagger.json",
"MiPos API v1"
);

c.RoutePrefix = "swagger";

});

app.UseAuthorization();

// Controllers
app.MapControllers();

// SignalR Hub
app.MapHub<PagoHub>("/pagohub");

app.Run();
