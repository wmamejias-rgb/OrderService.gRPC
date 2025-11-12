

using Microsoft.EntityFrameworkCore;
using OrderService.gRPC.Data;
using OrderService.gRPC.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/orderservice-.log", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Iniciando OrderService gRPC...");

    var builder = WebApplication.CreateBuilder(args);

    // Configurar Serilog como proveedor de logging
    builder.Host.UseSerilog();

    // Configurar Entity Framework Core con SQL Server
    builder.Services.AddDbContext<OrderDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
    });
    // Registrar validadores de FluentValidation



    // Registrar servicios gRPC
    builder.Services.AddGrpc(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.MaxReceiveMessageSize = 4 * 1024 * 1024; // 4 MB
        options.MaxSendMessageSize = 4 * 1024 * 1024;    // 4 MB
    });

    // Configurar reflexión de gRPC para herramientas de desarrollo
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddGrpcReflection();
    }

    var app = builder.Build();

    // Inicializar base de datos y datos de prueba
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<OrderDbContext>();
            var logger = services.GetRequiredService<ILogger<Program>>();
            await DbInitializer.InitializeAsync(context, logger);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error al inicializar la base de datos");
            throw;
        }
    }

    // Configurar pipeline HTTP
    if (app.Environment.IsDevelopment())
    {
        app.MapGrpcReflectionService();
    }

    // Mapear servicio gRPC
    app.MapGrpcService<OrderGrpcService>();

    // Endpoint de salud básico
    app.MapGet("/health", () => Results.Ok(new
    {
        status = "Healthy",
        service = "OrderService.gRPC",
        timestamp = DateTime.UtcNow
    }));

    // Endpoint de información del servicio
    app.MapGet("/", () => Results.Ok(new
    {
        service = "OrderService gRPC",
        version = "1.0.0",
        description = "Servicio de gestión de ordenes con comunicación gRPC",
       // endpoints = new[]       
        grpcPort = 7003,
        healthCheck = "/health"
    }));

    Log.Information("OrderService gRPC iniciado exitosamente en puerto 7003");
    await app.RunAsync();

}
catch (Exception ex)
{
    Log.Fatal(ex, "La aplicación falló al iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}




