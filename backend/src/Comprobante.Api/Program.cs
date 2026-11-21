using Comprobante.Api;
using Comprobante.Api.Catalogos;
using Comprobante.Catalogos;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var cadenaConexion = builder.Configuration.GetConnectionString("Catalogos")
    ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:Catalogos.");
builder.Services.AddCatalogosSat(cadenaConexion);
if (builder.Configuration.GetValue("Catalogos:SembrarAlIniciar", true))
{
    builder.Services.AddHostedService<SembradoAlIniciar>();
}

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapGet("/health", () => Results.Ok(new { estado = "ok" })).ExcludeFromDescription();
app.MapCatalogos();

app.Run();

// Para que las pruebas de integración arranquen la API con WebApplicationFactory<Program>.
public partial class Program;
