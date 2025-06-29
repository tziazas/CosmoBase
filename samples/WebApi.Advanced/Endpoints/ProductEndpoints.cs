using Microsoft.AspNetCore.Mvc;
using WebApi.Advanced.Services;
using System.ComponentModel.DataAnnotations;
using WebApi.Advanced.Models;

namespace WebApi.Advanced.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Products");
            //.RequireAuthorization(); // Uncomment to add authorization

        // Create a new product
        group.MapPost("/", CreateProduct)
            .WithName("CreateProduct")
            .WithSummary("Create a new product")
            .WithDescription("Creates a new product with the provided details")
            .Produces<ApiResponseDto<ProductResponseDto>>(StatusCodes.Status201Created)
            .Produces<ApiResponseDto<ProductResponseDto>>(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem();

        // Get a product by ID and category
        group.MapGet("/{id}", GetProduct)
            .WithName("GetProduct")
            .WithSummary("Get a product by ID")
            .WithDescription("Retrieves a product by its ID and category")
            .Produces<ApiResponseDto<ProductResponseDto>>()
            .Produces<ApiResponseDto<ProductResponseDto>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponseDto<ProductResponseDto>>(StatusCodes.Status500InternalServerError);

        // Update a product
        group.MapPut("/{id}", UpdateProduct)
            .WithName("UpdateProduct")
            .WithSummary("Update a product")
            .WithDescription("Updates an existing product with the provided details")
            .Produces<ApiResponseDto<ProductResponseDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponseDto<ProductResponseDto>>(StatusCodes.Status404NotFound)
            .Produces<ApiResponseDto<ProductResponseDto>>(StatusCodes.Status400BadRequest);

        // Delete a product (soft delete by default)
        group.MapDelete("/{id}", DeleteProduct)
            .WithName("DeleteProduct")
            .WithSummary("Delete a product")
            .WithDescription("Deletes a product (soft delete by default, hard delete optional)")
            .Produces<ApiResponseDto<object>>(StatusCodes.Status200OK)
            .Produces<ApiResponseDto<object>>(StatusCodes.Status400BadRequest);

        // Get products by category with pagination
        group.MapGet("/category/{category}", GetProductsByCategory)
            .WithName("GetProductsByCategory")
            .WithSummary("Get products by category")
            .WithDescription("Retrieves products for a specific category with pagination support")
            .Produces<ApiResponseDto<PagedResponseDto<ProductResponseDto>>>(StatusCodes.Status200OK)
            .Produces<ApiResponseDto<PagedResponseDto<ProductResponseDto>>>(StatusCodes.Status500InternalServerError);

        // Search products by name or description
        group.MapGet("/search", SearchProducts)
            .WithName("SearchProducts")
            .WithSummary("Search products")
            .WithDescription("Searches products by name or description, optionally filtered by category")
            .Produces<ApiResponseDto<IEnumerable<ProductResponseDto>>>(StatusCodes.Status200OK)
            .Produces<ApiResponseDto<IEnumerable<ProductResponseDto>>>(StatusCodes.Status500InternalServerError);

        // Bulk import products for a specific category
        group.MapPost("/bulk-import/{category}", BulkImportProducts)
            .WithName("BulkImportProducts")
            .WithSummary("Bulk import products")
            .WithDescription("Imports multiple products for a specific category in a single operation")
            .Produces<ApiResponseDto<object>>(StatusCodes.Status200OK)
            .Produces<ApiResponseDto<object>>(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> CreateProduct(
        [FromBody] CreateProductRequestDto request,
        IProductService productService,
        ILogger<Program> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await productService.CreateProductAsync(request, cancellationToken);
            
            return Results.Created($"/api/products/{product.Id}?category={product.Category}", 
                new ApiResponseDto<ProductResponseDto>
                {
                    Success = true,
                    Data = product,
                    Message = "Product created successfully"
                });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create product");
            return Results.BadRequest(new ApiResponseDto<ProductResponseDto>
            {
                Success = false,
                Message = "Failed to create product",
                Errors = { ex.Message }
            });
        }
    }

    private static async Task<IResult> GetProduct(
        string id,
        [FromQuery, Required] string category,
        IProductService productService,
        ILogger<Program> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await productService.GetProductByIdAsync(id, category, cancellationToken);
            
            if (product == null)
            {
                return Results.NotFound(new ApiResponseDto<ProductResponseDto>
                {
                    Success = false,
                    Message = $"Product {id} not found in category {category}"
                });
            }

            return Results.Ok(new ApiResponseDto<ProductResponseDto>
            {
                Success = true,
                Data = product
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve product {ProductId}", id);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Failed to retrieve product");
        }
    }

    private static async Task<IResult> UpdateProduct(
        string id,
        [FromBody] UpdateProductRequestDto request,
        IProductService productService,
        ILogger<Program> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await productService.UpdateProductAsync(id, request, cancellationToken);
            
            return Results.Ok(new ApiResponseDto<ProductResponseDto>
            {
                Success = true,
                Data = product,
                Message = "Product updated successfully"
            });
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new ApiResponseDto<ProductResponseDto>
            {
                Success = false,
                Message = $"Product {id} not found"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update product {ProductId}", id);
            return Results.BadRequest(new ApiResponseDto<ProductResponseDto>
            {
                Success = false,
                Message = "Failed to update product",
                Errors = { ex.Message }
            });
        }
    }

    private static async Task<IResult> DeleteProduct(
        string id,
        [FromQuery, Required] string category,
        [FromQuery] bool hardDelete,
        IProductService productService,
        ILogger<Program> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await productService.DeleteProductAsync(id, category, hardDelete, cancellationToken);
            
            return Results.Ok(new ApiResponseDto<object>
            {
                Success = true,
                Message = $"Product {id} deleted successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete product {ProductId}", id);
            return Results.BadRequest(new ApiResponseDto<object>
            {
                Success = false,
                Message = "Failed to delete product",
                Errors = { ex.Message }
            });
        }
    }

    private static async Task<IResult> GetProductsByCategory(
        string category,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        IProductService productService,
        ILogger<Program> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            page = Math.Max(1, page); // Ensure page is at least 1
            pageSize = Math.Clamp(pageSize, 1, 100); // Limit page size

            var products = await productService.GetProductsAsync(category, page, pageSize, cancellationToken);
            
            return Results.Ok(new ApiResponseDto<PagedResponseDto<ProductResponseDto>>
            {
                Success = true,
                Data = products
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve products for category {Category}", category);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Failed to retrieve products");
        }
    }

    private static async Task<IResult> SearchProducts(
        [FromQuery, Required] string searchTerm,
        [FromQuery] string? category,
        IProductService productService,
        ILogger<Program> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var products = await productService.SearchProductsAsync(searchTerm, category, cancellationToken);
            
            return Results.Ok(new ApiResponseDto<IEnumerable<ProductResponseDto>>
            {
                Success = true,
                Data = products
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search products with term {SearchTerm}", searchTerm);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Failed to search products");
        }
    }

    private static async Task<IResult> BulkImportProducts(
        string category,
        [FromBody] IEnumerable<CreateProductRequestDto> products,
        IProductService productService,
        ILogger<Program> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await productService.BulkImportProductsAsync(products, category, cancellationToken);
            
            return Results.Ok(new ApiResponseDto<object>
            {
                Success = true,
                Message = $"Successfully imported {products.Count()} products"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to bulk import products for category {Category}", category);
            return Results.BadRequest(new ApiResponseDto<object>
            {
                Success = false,
                Message = "Failed to import products",
                Errors = { ex.Message }
            });
        }
    }
}