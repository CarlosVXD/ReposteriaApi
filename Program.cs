using ReposteriaApi.Datos;
using ReposteriaApi.Datos.Oracle;
using ReposteriaApi.Infra;
using ReposteriaApi.Servicios;

var builder = WebApplication.CreateBuilder(args);

// Dónde se guardan los datos: "Oracle" (real) o "Memoria" (pruebas, sin base de datos).
// Se cambia en appsettings.json -> Datos:Proveedor
var proveedor = builder.Configuration["Datos:Proveedor"] ?? "Oracle";
if (proveedor.Equals("Memoria", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IRepositorio, RepositorioMemoria>();
else
    builder.Services.AddRepositorioOracle(builder.Configuration);

builder.Services.AddSingleton(TimeProvider.System);
// Servicio de NLP (Python + spaCy). Su dirección está en appsettings.json -> Nlp:Url
builder.Services.AddHttpClient<NlpClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Nlp:Url"] ?? "http://localhost:8000");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<NegocioService>();
builder.Services.AddControllers();

// Permite que Angular (ng serve, puerto 4200) llame a esta API
builder.Services.AddCors(o => o.AddPolicy("angular", p => p
    .WithOrigins("http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

app.UseMiddleware<ManejadorErrores>();
app.UseCors("angular");
app.MapControllers();

// Si la base de datos está vacía, la llena con los datos de ejemplo
using (var alcance = app.Services.CreateScope())
{
    var repo = alcance.ServiceProvider.GetRequiredService<IRepositorio>();
    if (await repo.EstaVacioAsync()) await Semilla.AplicarAsync(repo.InsertarAsync, DateTime.Now);
}

app.Run();
