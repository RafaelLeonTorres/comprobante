using System.Text.Json.Serialization;
using Comprobante.Api;
using Comprobante.Api.Catalogos;
using Comprobante.Api.Comprobantes;
using Comprobante.Api.Errores;
using Comprobante.Catalogos;
using Comprobante.Core.Calculo;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var cadenaConexion = builder.Configuration.GetConnectionString("Catalogos")
    ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:Catalogos.");
builder.Services.AddCatalogosSat(cadenaConexion);
if (builder.Configuration.GetValue("Catalogos:SembrarAlIniciar", true))
{
    builder.Services.AddHostedService<SembradoAlIniciar>();
}

builder.Services.AddSingleton<ServicioCalculo>();

builder.Services.ConfigureHttpJsonOptions(opciones =>
    opciones.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

// Un JSON mal formado debe dar el mismo 400 en cualquier entorno (por omisión, en Development lanza la excepción).
builder.Services.Configure<RouteHandlerOptions>(opciones => opciones.ThrowOnBadRequest = false);
builder.Services.AddProblemasEnEspanol();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// La documentación vive bajo /api para que nginx la sirva junto con el resto de la API.
app.MapOpenApi("/api/openapi/{documentName}.json");
app.MapScalarApiReference("/api/scalar", opciones =>
    opciones.WithTitle("Comprobante: API").WithOpenApiRoutePattern("/api/openapi/{documentName}.json"));

app.MapGet("/api/health", () => Results.Ok(new { estado = "ok" }))
    .WithTags("Salud")
    .WithSummary("Responde ok cuando la API ya arrancó y cargó los catálogos.");
app.MapCatalogos();
app.MapComprobantes();

app.Run();

// Para que las pruebas de integración arranquen la API con WebApplicationFactory<Program>.
public partial class Program;
