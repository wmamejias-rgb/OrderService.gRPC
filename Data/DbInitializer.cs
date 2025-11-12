

using Microsoft.EntityFrameworkCore;
using OrderService.Models;


namespace OrderService.gRPC.Data
{
    public class DbInitializer
    {


        public static async Task InitializeAsync(OrderDbContext context, ILogger logger)
        {
            try
            {
                // Asegurar que la base de datos esté creada
                logger.LogInformation("Verificando existencia de base de datos...");
                await context.Database.EnsureCreatedAsync();

                // Aplicar migraciones pendientes
                if (context.Database.GetPendingMigrations().Any())
                {
                    logger.LogInformation("Aplicando migraciones pendientes...");
                    await context.Database.MigrateAsync();
                }

                // Verificar si ya existen Orders
                if (await context.Orders.AnyAsync())
                {
                    logger.LogInformation("Base de datos ya contiene orders. Omitiendo inicialización.");
                    return;
                }

                // Verificar si ya existen OrderItems
                if (await context.OrderItems.AnyAsync())
                {
                    logger.LogInformation("Base de datos ya contiene OrderItems. Omitiendo inicialización.");
                    return;
                }

                logger.LogInformation("Inicializando base de datos con datos de prueba...");

                var orders = new List<Order>
                {
                    new Order
                    {
                       UserId =  1,
                       ShippingAddress = "San jose",
                        Items = new List<OrderItem> {
                        new OrderItem
                        {
                            OrderId = 1,
                            ProductId = 1,
                            ProductName = "Tablet",
                            Quantity = 1,
                            UnitPrice   = 120,
                        }
                       },                       
                    },
                    new Order
                    {
                       UserId =  1,
                       ShippingAddress = "San jose",
                       Items = new List<OrderItem> {
                       new OrderItem
                        {
                            OrderId = 2,
                            ProductId = 1,
                            ProductName = "Tablet",
                            Quantity = 1,
                            UnitPrice   = 120,
                        }
                       },
                    }
                };

                // Agregar usuarios a la base de datos
                await context.Orders.AddRangeAsync(orders);
                await context.SaveChangesAsync();

                logger.LogInformation($"Base de datos inicializada exitosamente con {orders.Count} ordenes.");
            }

            catch (Exception ex)
            {
                logger.LogError(ex, "Error al inicializar la base de datos.");
                throw;
            }

        }
    }
}
