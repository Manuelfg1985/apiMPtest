using MercadoPago.Config;
using MiPos.API.Hubs;
using MiPos.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ================================
// PUERTO PARA RENDER
// ================================

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";

builder.WebHost.UseUrls($"[http://0.0.0.0:{port}](http://0.0.0.0:{port})");

// ================================
// MERCADO PAGO
// ================================

var accessToken =
builder.Configuration["MercadoPago:AccessToken"]
?? Environment.GetEnvironmentVariable("MercadoPago__AccessToken")
?? "";

if (!string.IsNullOrWhiteSpace(accessToken))
{
MercadoPagoConfig.AccessToken = accessToken;


Console.WriteLine("[INIT] MercadoPago configurado correctamente.");


}
else
{
Console.WriteLine("[ERROR] No se encontró MercadoPago AccessToken.");
}

// ================================
// SMTP - DEBUG DE CONFIGURACIÓN
// ================================

Console.WriteLine(
$"[SMTP] Host configurado: {!string.IsNullOrEmpty(builder.Configuration["Smtp:Host"])}"
);

Console.WriteLine(
$"[SMTP] Puerto: {builder.Configuration["Smtp:Port"]}"
);

Console.WriteLine(
$"[SMTP] Usuario configurado: {!string.IsNullOrEmpty(builder.Configuration["Smtp:Username"])}"
);

Console.WriteLine(
$"[SMTP] Password configurado: {!string.IsNullOrEmpty(builder.Configuration["Smtp:Password"])}"
);

// ================================
// CORS
// ================================

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

// ================================
// SERVICIOS
// ================================

builder.Services.AddControllers();

builder.Services.AddSignalR();

builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

// ================================
// CONSTRUIR APLICACIÓN
// ================================

var app = builder.Build();

// ================================
// MIDDLEWARE
// ================================

app.UseCors("AllowAll");

// ================================
// SWAGGER
// ================================

app.UseSwagger();

app.UseSwaggerUI(c =>
{
c.SwaggerEndpoint(
"/swagger/v1/swagger.json",
"MiPos API v1"
);


c.RoutePrefix = "swagger";


});

// ================================
// RUTA PRINCIPAL
// ================================

app.MapGet("/", () =>
{
return Results.Ok(new
{
status = "online",
service = "MiPos API"
});
});

// ================================
// ENDPOINTS
// ================================

app.UseAuthorization();

app.MapControllers();

app.MapHub<PagoHub>("/pagohub");

// ================================
// INICIAR API
// ================================

app.Run();
