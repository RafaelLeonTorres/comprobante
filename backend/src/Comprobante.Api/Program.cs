using System.Text.Json.Serialization;
using Comprobante.Api;
using Comprobante.Api.Catalogos;
using Comprobante.Api.Comprobantes;
using Comprobante.Api.Errores;
using Comprobante.Api.Representaciones;
using Comprobante.Api.Seguridad;
using Comprobante.Api.Validaciones;
using Comprobante.Catalogos;
using Comprobante.Core.Calculo;
using Comprobante.Core.Generacion;
using Comprobante.Core.Representacion;
using Comprobante.Core.Sello;
using Comprobante.Core.Validacion;
using Microsoft.AspNetCore.Http.Features;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(kestrel =>
{
    // Sin "Server: Kestrel". El tope es un respaldo: ningún endpoint recibe más de 2 MiB (el XML a validar), y lo que
    // pase de 3 MiB no es un uso legítimo. El del JSON de cálculo y generación lo aplica LimiteDeCuerpoJson.
    kestrel.AddServerHeader = false;
    kestrel.Limits.MaxRequestBodySize = 3 * 1024 * 1024;
});

var cadenaConexion = builder.Configuration.GetConnectionString("Catalogos")
    ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:Catalogos.");
builder.Services.AddCatalogosSat(cadenaConexion);
if (builder.Configuration.GetValue("Catalogos:SembrarAlIniciar", true))
{
    builder.Services.AddHostedService<SembradoAlIniciar>();
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ServicioCalculo>();
builder.Services.AddSingleton<EmisoresDePrueba>();
builder.Services.AddSingleton<ValidadorCfdi>();
builder.Services.AddSingleton<ServicioGeneracion>();
builder.Services.AddSingleton<ConstructorDeRepresentacion>();
builder.Services.AddSingleton<ServicioDePdf>();

// El único formulario de la API es el del PDF (XML y logo): hasta tres campos, todo en memoria (sin archivos temporales
// en disco) y nada más grande de lo que ese endpoint acepta.
builder.Services.Configure<FormOptions>(opciones =>
{
    opciones.ValueCountLimit = 8;
    opciones.MultipartBodyLengthLimit = EndpointsRepresentaciones.TamanoMaximoDelFormularioEnBytes;
    opciones.MemoryBufferThreshold = EndpointsRepresentaciones.TamanoMaximoDelFormularioEnBytes;
    opciones.BufferBodyLengthLimit = EndpointsRepresentaciones.TamanoMaximoDelFormularioEnBytes;
});

builder.Services.ConfigureHttpJsonOptions(opciones =>
    opciones.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

// Un JSON mal formado debe dar el mismo 400 en cualquier entorno (por omisión, en Development lanza la excepción).
builder.Services.Configure<RouteHandlerOptions>(opciones => opciones.ThrowOnBadRequest = false);
builder.Services.AddProblemasEnEspanol();
builder.Services.AddLimiteDeSolicitudes();
builder.Services.AddOpenApi(opciones => opciones.DescribirElPdf());

var app = builder.Build();

// Primero el registro, para que vea el estado final; después los errores en español y los límites: el de solicitudes por
// IP (429) y el del cuerpo del JSON (413), antes de leer o procesar nada.
app.UseMiddleware<RegistroDeSolicitudes>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();
app.UseWhen(LimiteDeCuerpoJson.Aplica, rama => rama.UseMiddleware<LimiteDeCuerpoJson>());

// La documentación vive bajo /api para que nginx la sirva junto con el resto de la API.
app.MapOpenApi("/api/openapi/{documentName}.json");
app.MapScalarApiReference("/api/scalar", opciones =>
    opciones.WithTitle("Comprobante: API").WithOpenApiRoutePattern("/api/openapi/{documentName}.json"));

app.MapGet("/api/health", () => Results.Ok(new { estado = "ok" }))
    .WithTags("Salud")
    .WithSummary("Responde ok cuando la API ya arrancó y cargó los catálogos.");
app.MapCatalogos();
app.MapComprobantes();
app.MapValidaciones();
app.MapRepresentaciones();

app.Run();

// Para que las pruebas de integración arranquen la API con WebApplicationFactory<Program>.
public partial class Program;
