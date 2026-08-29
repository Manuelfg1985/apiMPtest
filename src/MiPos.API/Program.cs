using MercadoPago.Config;
using MiPos.API.Hubs;
using MiPos.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// PUERTO PARA RENDER / DOCKER
// ==========================================

var port = Environment.GetEnvironmentVariable("PORT");

if (string.IsNullOrWhiteSpace(port))
{
    port = "8080";
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(int.Parse(port));
});


// ==========================================
// MERCADO PAGO
// ==========================================

var accessToken =
    builder.Configuration["MercadoPago:AccessToken"]
    ?? "";

if (!string.IsNullOrWhiteSpace(accessToken))
{
    MercadoPagoConfig.AccessToken = accessToken;

    Console.WriteLine(
        "[INIT] MercadoPago configurado correctamente."
    );
}
else
{
    Console.WriteLine(
        "[ERROR] No se encontró MercadoPago AccessToken."
    );
}


// ==========================================
// SMTP - VERIFICACIÓN DE CONFIGURACIÓN
// ==========================================

var smtpHost = builder.Configuration["Smtp:Host"];
var smtpPort = builder.Configuration["Smtp:Port"];
var smtpUsername = builder.Configuration["Smtp:Username"];
var smtpPassword = builder.Configuration["Smtp:Password"];

Console.WriteLine(
    $"[SMTP] Host configurado: {!string.IsNullOrWhiteSpace(smtpHost)}"
);

Console.WriteLine(
    $"[SMTP] Puerto: {smtpPort ?? "NO CONFIGURADO"}"
);

Console.WriteLine(
    $"[SMTP] Usuario configurado: {!string.IsNullOrWhiteSpace(smtpUsername)}"
);

Console.WriteLine(
    $"[SMTP] Password configurado: {!string.IsNullOrWhiteSpace(smtpPassword)}"
);


// ==========================================
// CORS
// ==========================================

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


// ==========================================
// SERVICIOS
// ==========================================

builder.Services.AddControllers();

builder.Services.AddSignalR();

builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();


// ==========================================
// CONSTRUIR APLICACIÓN
// ==========================================

var app = builder.Build();


// ==========================================
// MIDDLEWARE
// ==========================================

app.UseCors("AllowAll");


// ==========================================
// SWAGGER
// ==========================================

app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint(
        "/swagger/v1/swagger.json",
        "MiPos API v1"
    );

    c.RoutePrefix = "swagger";
});


// ==========================================
// RUTA PRINCIPAL / HEALTH CHECK
// ==========================================

app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        status = "online",
        service = "MiPos API"
    });
});


// ==========================================
// ENDPOINTS
// ==========================================

app.UseAuthorization();

app.MapControllers();

app.MapHub<PagoHub>("/pagohub");


// ==========================================
// INICIAR API
// ==========================================

app.Run();