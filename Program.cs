using MercadoPago.Config;
using MiPos.API.Hubs;
using MiPos.API.Services;

var builder = WebApplication.CreateBuilder(args);
MercadoPagoConfig.AccessToken = builder.Configuration["MercadoPago:AccessToken"];

string accessToken = builder.Configuration["MercadoPago:AccessToken"] 
    ?? "TEST-TU-ACCESS-TOKEN-DE-PRUEBA";

MercadoPagoConfig.AccessToken = accessToken;

builder.Services.AddControllers();
builder.Services.AddSignalR(); // <--- Habilitar SignalR
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();
app.MapHub<PagoHub>("/pagohub"); // <--- Mapear Endpoint de WebSockets

app.Run();