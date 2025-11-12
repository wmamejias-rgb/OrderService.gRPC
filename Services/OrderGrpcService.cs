using Azure.Core;
using ECommerceGRPC.OrderService;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using OrderService.gRPC.Data;
using OrderService.Models;


namespace OrderService.gRPC.Services
{
    public class OrderGrpcService : global::ECommerceGRPC.OrderService.OrderService.OrderServiceBase
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<OrderGrpcService> _logger;

        public OrderGrpcService(OrderDbContext context, ILogger<OrderGrpcService> logger)
        {
            _logger = logger;
            _context = context;
        }

        public override async Task<OrderResponse> CreateOrder(CreateOrderRequest request, ServerCallContext context)
        {
            var order = new Order
            {
                UserId = request.UserId,
                ShippingAddress = request.ShippingAddress,
                OrderDate = DateTime.UtcNow,
                Status = "Pending",
                Items = request.Items.Select(i => new OrderItem
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = decimal.Parse(i.UnitPrice)
                }).ToList()
            };

            // Calcular total
            order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Pedido creado con ID {OrderId} via gRPC", order.Id);

            return MapToProtoResponse(order);
        }

        public override async Task<OrderResponse> GetOrder(GetOrderRequest request, ServerCallContext context)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == request.Id);

            if (order == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"Pedido con ID {request.Id} no encontrado"));
            }

            return MapToProtoResponse(order);
        }

        public override async Task<OrderResponse> UpdateOrderStatus(UpdateOrderStatusRequest request, ServerCallContext context)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == request.Id);

            if (order == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"Pedido con ID {request.Id} no encontrado"));
            }

            order.Status = request.Status;
            order.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(request.PaymentTransactionId))
            {
                order.PaymentTransactionId = request.PaymentTransactionId;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Pedido {OrderId} actualizado a {Status} via gRPC", request.Id, request.Status);

            return MapToProtoResponse(order);
        }

        public override async Task<ListOrdersResponse> ListOrders(ListOrdersRequest request, ServerCallContext context)
        {
            var query = _context.Orders
                .Include(o => o.Items)
                .AsQueryable();

            // Filtrar por usuario si se especifica
            if (request.UserId > 0)
            {
                query = query.Where(o => o.UserId == request.UserId);
            }

            var totalCount = await query.CountAsync();

            var page = request.Page > 0 ? request.Page : 1;
            var pageSize = request.PageSize > 0 ? request.PageSize : 10;

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var response = new ListOrdersResponse
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            response.Orders.AddRange(orders.Select(MapToProtoResponse));

            return response;
        }

        public override async Task<OrderResponse> CancelOrder(CancelOrderRequest request, ServerCallContext context)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == request.Id);

            if (order == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"Pedido con ID {request.Id} no encontrado"));
            }

            if (order.Status == "Shipped" || order.Status == "Delivered")
            {
                throw new RpcException(new Status(StatusCode.FailedPrecondition,
                    "No se puede cancelar un pedido que ya fue enviado o entregado"));
            }

            order.Status = "Cancelled";
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Pedido {OrderId} cancelado via gRPC", request.Id);

            return MapToProtoResponse(order);
        }

        private static OrderResponse MapToProtoResponse(Order order)
        {
            var response = new OrderResponse
            {
                Id = order.Id,
                UserId = order.UserId,
                OrderDate = Timestamp.FromDateTime(order.OrderDate.ToUniversalTime()),
                Status = order.Status,
                TotalAmount = order.TotalAmount.ToString("F2"),
                ShippingAddress = order.ShippingAddress
            };

            if (order.PaymentTransactionId != null)
            {
                response.PaymentTransactionId = order.PaymentTransactionId;
            }

            if (order.UpdatedAt.HasValue)
            {
                response.UpdatedAt = Timestamp.FromDateTime(order.UpdatedAt.Value.ToUniversalTime());
            }

            response.Items.AddRange(order.Items.Select(i => new OrderItemMessage
            {
                Id = i.Id,
                OrderId = i.OrderId,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice.ToString("F2"),
                Subtotal = i.Subtotal.ToString("F2")
            }));

            return response;
        }
    }
}
