using Microsoft.AspNetCore.Mvc;
using WebApi.Advanced.Services;
using WebApi.Advanced.Models;
using System.ComponentModel.DataAnnotations;

namespace WebApi.Advanced.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders")
            .WithTags("Orders");
        //.RequireAuthorization(); // Uncomment to add authorization

        // Create a new order
        group.MapPost("/", CreateOrder)
            .WithName("CreateOrder")
            .WithSummary("Create a new order")
            .WithDescription("Creates a new order for a customer with the provided items")
            .Produces<ApiResponseDto<OrderResponseDto>>(StatusCodes.Status201Created)
            .Produces<ApiResponseDto<OrderResponseDto>>(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem();

        // Get an order by ID and customer ID
        group.MapGet("/{id}", GetOrder)
            .WithName("GetOrder")
            .WithSummary("Get an order by ID")
            .WithDescription("Retrieves an order by its ID and customer ID")
            .Produces<ApiResponseDto<OrderResponseDto>>()
            .Produces<ApiResponseDto<OrderResponseDto>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponseDto<OrderResponseDto>>(StatusCodes.Status500InternalServerError);

        // Update order status
        group.MapPatch("/{id}/status", UpdateOrderStatus)
            .WithName("UpdateOrderStatus")
            .WithSummary("Update order status")
            .WithDescription("Updates the status of an existing order with validation of status transitions")
            .Produces<ApiResponseDto<OrderResponseDto>>()
            .Produces<ApiResponseDto<OrderResponseDto>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponseDto<OrderResponseDto>>(StatusCodes.Status400BadRequest);

        // Cancel an order
        group.MapDelete("/{id}", CancelOrder)
            .WithName("CancelOrder")
            .WithSummary("Cancel an order")
            .WithDescription("Cancels an order (soft delete) if it's in a cancellable status")
            .Produces<ApiResponseDto<object>>()
            .Produces<ApiResponseDto<object>>(StatusCodes.Status400BadRequest);

        // Get orders for a customer with pagination
        group.MapGet("/customer/{customerId}", GetOrdersForCustomer)
            .WithName("GetOrdersForCustomer")
            .WithSummary("Get orders for a customer")
            .WithDescription("Retrieves all orders for a specific customer with pagination support")
            .Produces<ApiResponseDto<PagedResponseDto<OrderResponseDto>>>()
            .Produces<ApiResponseDto<PagedResponseDto<OrderResponseDto>>>(StatusCodes.Status500InternalServerError);

        // Get order summary for a customer
        var routeHandlerBuilder = group.MapGet("/customer/{customerId}/summary", GetOrderSummary);
        routeHandlerBuilder.WithName("GetOrderSummary");
        routeHandlerBuilder.WithSummary("Get order summary for a customer");
        routeHandlerBuilder.WithDescription("Retrieves aggregated order statistics for a specific customer");
        routeHandlerBuilder.Produces<ApiResponseDto<OrderSummaryDto>>();
        routeHandlerBuilder.Produces<ApiResponseDto<OrderSummaryDto>>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> CreateOrder(
        [FromBody] CreateOrderRequestDto request,
        IOrderService orderService,
        ILogger<Program> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var order = await orderService.CreateOrderAsync(request, cancellationToken);
            
            return Results.Created($"/api/orders/{order.Id}?customerId={order.CustomerId}", 
                new ApiResponseDto<OrderResponseDto>
                {
                    Success = true,
                    Data = order,
                    Message = "Order created successfully"
                });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create order for customer {CustomerId}", request.CustomerId);
            return Results.BadRequest(new ApiResponseDto<OrderResponseDto>
            {
                Success = false,
                Message = "Failed to create order",
                Errors = { ex.Message }
            });
        }
    }

    private static async Task<IResult> GetOrder(
        string id,
        [FromQuery, Required] string customerId,
        IOrderService orderService,
        ILogger<Program> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var order = await orderService.GetOrderByIdAsync(id, customerId, cancellationToken);
            
            if (order == null)
            {
                return Results.NotFound(new ApiResponseDto<OrderResponseDto>
                {
                    Success = false,
                    Message = $"Order {id} not found for customer {customerId}"
                });
            }

            return Results.Ok(new ApiResponseDto<OrderResponseDto>
            {
                Success = true,
                Data = order
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve order {OrderId} for customer {CustomerId}", id, customerId);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Failed to retrieve order");
        }
    }

    private static async Task<IResult> UpdateOrderStatus(
        string id,
        [FromQuery, Required] string customerId,
        [FromBody] UpdateOrderStatusRequestDto request,
        IOrderService orderService,
        ILogger<Program> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var order = await orderService.UpdateOrderStatusAsync(id, customerId, request.NewStatus, cancellationToken);
            
            return Results.Ok(new ApiResponseDto<OrderResponseDto>
            {
                Success = true,
                Data = order,
                Message = $"Order status updated to {request.NewStatus}"
            });
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new ApiResponseDto<OrderResponseDto>
            {
                Success = false,
                Message = $"Order {id} not found for customer {customerId}"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ApiResponseDto<OrderResponseDto>
            {
                Success = false,
                Message = "Invalid status transition",
                Errors = { ex.Message }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update order {OrderId} status", id);
            return Results.BadRequest(new ApiResponseDto<OrderResponseDto>
            {
                Success = false,
                Message = "Failed to update order status",
                Errors = { ex.Message }
            });
        }
    }

    private static async Task<IResult> CancelOrder(
        string id,
        [FromQuery, Required] string customerId,
        IOrderService orderService,
        ILogger<Program> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await orderService.CancelOrderAsync(id, customerId, cancellationToken);
            
            return Results.Ok(new ApiResponseDto<object>
            {
                Success = true,
                Message = $"Order {id} cancelled successfully"
            });
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new ApiResponseDto<object>
            {
                Success = false,
                Message = $"Order {id} not found for customer {customerId}"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ApiResponseDto<object>
            {
                Success = false,
                Message = "Cannot cancel order",
                Errors = { ex.Message }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cancel order {OrderId}", id);
            return Results.BadRequest(new ApiResponseDto<object>
            {
                Success = false,
                Message = "Failed to cancel order",
                Errors = { ex.Message }
            });
        }
    }

    private static async Task<IResult> GetOrdersForCustomer(
        string customerId,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        IOrderService orderService,
        ILogger<Program> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            page = Math.Max(1, page); // Ensure page is at least 1
            pageSize = Math.Clamp(pageSize, 1, 50); // Limit page size for orders

            var orders = await orderService.GetOrdersForCustomerAsync(customerId, page, pageSize, cancellationToken);
            
            return Results.Ok(new ApiResponseDto<PagedResponseDto<OrderResponseDto>>
            {
                Success = true,
                Data = orders
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve orders for customer {CustomerId}", customerId);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Failed to retrieve orders");
        }
    }

    private static async Task<IResult> GetOrderSummary(
        string customerId,
        IOrderService orderService,
        ILogger<Program> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var summary = await orderService.GetOrderSummaryAsync(customerId, cancellationToken);
            
            return Results.Ok(new ApiResponseDto<OrderSummaryDto>
            {
                Success = true,
                Data = summary
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve order summary for customer {CustomerId}", customerId);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Failed to retrieve order summary");
        }
    }
}