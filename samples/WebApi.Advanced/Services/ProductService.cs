using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Abstractions.Enums;
using CosmoBase.Abstractions.Filters;
using WebApi.Advanced.Models;
using Microsoft.Extensions.Logging;

namespace WebApi.Advanced.Services;

public class ProductService(
    ICosmosDataReadService<Product, ProductDao> readService,
    ICosmosDataWriteService<Product, ProductDao> writeService,
    ILogger<ProductService> logger)
    : IProductService
{
    public async Task<ProductResponseDto> CreateProductAsync(CreateProductRequestDto request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Creating product {ProductName} in category {Category}", request.Name, request.Category);

        var product = new Product
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Category = request.Category,
            Price = request.Price,
            Description = request.Description,
            Tags = request.Tags ?? new List<string>(),
            StockQuantity = request.InitialStock,
            Sku = request.Sku,
            IsActive = true
        };

        var created = await writeService.CreateAsync(product, cancellationToken);
        
        logger.LogInformation("Successfully created product {ProductId}", created.Id);
        
        return MapToResponseDto(created);
    }

    public async Task<ProductResponseDto?> GetProductByIdAsync(string id, string category, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving product {ProductId} from category {Category}", id, category);

        var product = await readService.GetByIdAsync(id, category, includeDeleted: false, cancellationToken);
        
        if (product == null)
        {
            logger.LogWarning("Product {ProductId} not found in category {Category}", id, category);
            return null;
        }

        return MapToResponseDto(product);
    }

    public async Task<ProductResponseDto> UpdateProductAsync(string id, UpdateProductRequestDto request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Updating product {ProductId}", id);

        var existing = await readService.GetByIdAsync(id, request.Category, includeDeleted: false, cancellationToken);
        if (existing == null)
        {
            throw new KeyNotFoundException($"Product {id} not found");
        }

        // Update properties
        existing.Name = request.Name ?? existing.Name;
        existing.Price = request.Price ?? existing.Price;
        existing.Description = request.Description ?? existing.Description;
        existing.StockQuantity = request.StockQuantity ?? existing.StockQuantity;
        existing.IsActive = request.IsActive ?? existing.IsActive;

        if (request.Tags != null)
        {
            existing.Tags = request.Tags;
        }

        var updated = await writeService.ReplaceAsync(existing, cancellationToken);
        
        logger.LogInformation("Successfully updated product {ProductId}", id);
        
        return MapToResponseDto(updated);
    }

    public async Task DeleteProductAsync(string id, string category, bool hardDelete = false, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting product {ProductId} (hard delete: {HardDelete})", id, hardDelete);

        var deleteOption = hardDelete ? DeleteOptions.HardDelete : DeleteOptions.SoftDelete;
        await writeService.DeleteAsync(id, category, deleteOption, cancellationToken);
        
        logger.LogInformation("Successfully deleted product {ProductId}", id);
    }

    public async Task<PagedResponseDto<ProductResponseDto>> GetProductsAsync(string category, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving products for category {Category}, page {Page}, size {PageSize}", category, page, pageSize);

        var spec = new SqlSpecification<Product>(
            "SELECT * FROM c WHERE c.Category = @category ORDER BY c.Name",
            new Dictionary<string, object> { ["@category"] = category });

        string? continuationToken = null;
        
        // For pages beyond the first, we'd need to store/retrieve continuation tokens
        // This is a simplified implementation
        var (items, nextToken, totalCount) = await readService.GetPageWithTokenAndCountAsync(
            spec, category, pageSize, continuationToken, cancellationToken);

        var productDtos = items.Select(MapToResponseDto).ToList();

        return new PagedResponseDto<ProductResponseDto>
        {
            Items = productDtos,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount ?? 0,
            HasNextPage = !string.IsNullOrEmpty(nextToken)
        };
    }

    public async Task<IEnumerable<ProductResponseDto>> SearchProductsAsync(string searchTerm, string? category = null, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Searching products with term {SearchTerm} in category {Category}", searchTerm, category);

        var whereClause = "CONTAINS(LOWER(c.Name), LOWER(@searchTerm)) OR CONTAINS(LOWER(c.Description), LOWER(@searchTerm))";
        var parameters = new Dictionary<string, object> { ["@searchTerm"] = searchTerm };

        if (!string.IsNullOrEmpty(category))
        {
            whereClause += " AND c.Category = @category";
            parameters["@category"] = category;
        }

        var spec = new SqlSpecification<Product>(
            $"SELECT * FROM c WHERE {whereClause} ORDER BY c.Name",
            parameters);

        var results = new List<Product>();
        
        if (!string.IsNullOrEmpty(category))
        {
            await foreach (var product in readService.QueryAsync(spec, cancellationToken))
            {
                results.Add(product);
            }
        }
        else
        {
            await foreach (var product in readService.QueryAsync(spec, cancellationToken))
            {
                results.Add(product);
            }
        }

        logger.LogInformation("Found {Count} products matching search term {SearchTerm}", results.Count, searchTerm);
        
        return results.Select(MapToResponseDto);
    }

    public async Task BulkImportProductsAsync(IEnumerable<CreateProductRequestDto> products, string category, CancellationToken cancellationToken = default)
    {
        var productList = products.ToList();
        logger.LogInformation("Starting bulk import of {Count} products for category {Category}", productList.Count, category);

        var productsToImport = productList.Select(dto => new Product
        {
            Id = Guid.NewGuid().ToString(),
            Name = dto.Name,
            Category = category, // Force the category
            Price = dto.Price,
            Description = dto.Description,
            Tags = dto.Tags ?? new List<string>(),
            StockQuantity = dto.InitialStock,
            Sku = dto.Sku,
            IsActive = true
        }).ToList();

        await writeService.BulkInsertAsync(
            productsToImport,
            p => p.Category,
            configureItem: p => p.Tags.Add("bulk-imported"),
            batchSize: 25,
            maxConcurrency: 5,
            cancellationToken);

        logger.LogInformation("Successfully completed bulk import of {Count} products", productList.Count);
    }

    private static ProductResponseDto MapToResponseDto(Product product)
    {
        return new ProductResponseDto
        {
            Id = product.Id,
            Name = product.Name,
            Category = product.Category,
            Price = product.Price,
            Description = product.Description,
            Tags = product.Tags,
            StockQuantity = product.StockQuantity,
            Sku = product.Sku,
            IsActive = product.IsActive,
            CreatedOnUtc = product.CreatedOnUtc,
            UpdatedOnUtc = product.UpdatedOnUtc,
            CreatedBy = product.CreatedBy,
            UpdatedBy = product.UpdatedBy
        };
    }
}