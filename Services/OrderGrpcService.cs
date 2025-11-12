using Azure.Core;
using ECommerceGRPC.OrderService;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using OrderService.gRPC.Data;
using OrderService.Models;

namespace OrderService.gRPC.Services
{
    public class OrderGrpcService: global::ECommerceGRPC.OrderService.OrderService.OrderServiceBase
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<OrderGrpcService> _logger;

        public OrderGrpcService(  OrderDbContext context,    ILogger<OrderGrpcService> logger)
        {
            _logger = logger;
            _context = context;
        }


        public override async Task<OrderResponse> GetOrderById (GetOrderByIdRequest request, ServerCallContext context)
        {

            try
            {
                _logger.LogInformation($"GetOrderById llamado para ID: {request.Id}");

                // Validar solicitud mayor a cero
              /*  var validationResult = await _getUserValidator.ValidateAsync(request);
               if (!validationResult.IsValid)
                {
                    var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                    _logger.LogWarning($"Validación fallida: {errors}");
                    throw new RpcException(new Status(StatusCode.InvalidArgument, errors));
                }
              */
                //Busca si orden existe en base de datos
                var order = await _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == request.Id);
                if (order == null)
                {
                    _logger.LogWarning($"Order con {request.Id} no se encontró");
                    throw new RpcException(new Status(StatusCode.NotFound,
                        $"Order con ID {request.Id} no existe"));
                }

                _logger.LogInformation($"Order {order.Id} encontrado exitosamente");

                return MapToOrderResponse(order);

            }
            catch (RpcException)
            {
                throw;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener usuario con ID {request.Id}");
                throw new RpcException(new Status(StatusCode.Internal,
                    "Error interno al procesar la solicitud"));
            }


        }

        
        private OrderResponse MapToOrderResponse(Order order)
        {

            var responseOrdeItems = new OrdenItemResponse();

            IEnumerable<OrderItem> items = order.Items.Select(i => new OrderItem
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            });

            var responseOrder = new OrderResponse
            {
                Id = order.Id,
                UserId = order.UserId,
                OrderDate = order.OrderDate.ToString("o"),
                Status = order.Status,
                TotalAmount = Convert.ToDouble(order.TotalAmount),
                ShippingAddress = order.ShippingAddress,                
                PaymentTransactionId = order.PaymentTransactionId ?? string.Empty,
                UpdatedAt = order.UpdatedAt?.ToString("o") ?? string.Empty
            };

            responseOrder.Items.Add(responseOrdeItems); 

            return responseOrder;   
        }
    }
}
