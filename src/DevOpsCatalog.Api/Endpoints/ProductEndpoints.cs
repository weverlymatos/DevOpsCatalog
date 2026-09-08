using System.Text.Json;
using DevOpsCatalog.Api.Domain;
using DevOpsCatalog.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace DevOpsCatalog.Api.Endpoints;

public static class ProductEndpoints
{
    private const string CacheKey = "products:all";

    public static IEndpointRouteBuilder MapProductEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/products")
            .WithTags("Products");

        group.MapGet("/", GetAllAsync);

        group.MapGet("/{id:guid}", GetByIdAsync);

        group.MapPost("/", CreateAsync);

        group.MapPut("/{id:guid}", UpdateAsync);

        group.MapDelete("/{id:guid}", DeleteAsync);

        return endpoints;
    }

    private static async Task<IResult> GetAllAsync(
        CatalogDbContext dbContext,
        IDistributedCache cache,
        CancellationToken cancellationToken)
    {
        var cached = await cache.GetStringAsync(
            CacheKey,
            cancellationToken);

        if (cached is not null)
        {
            var products = JsonSerializer.Deserialize<List<Product>>(cached);

            return Results.Ok(products);
        }

        var result = await dbContext.Products
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        await cache.SetStringAsync(
            CacheKey,
            JsonSerializer.Serialize(result),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromMinutes(5)
            },
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        CatalogDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        return product is null
            ? Results.NotFound()
            : Results.Ok(product);
    }

    private static async Task<IResult> CreateAsync(
        CreateProductRequest request,
        CatalogDbContext dbContext,
        IDistributedCache cache,
        CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Price = request.Price,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Products.Add(product);

        await dbContext.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(
            CacheKey,
            cancellationToken);

        return Results.Created(
            $"/products/{product.Id}",
            product);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CatalogDbContext dbContext,
        IDistributedCache cache,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (product is null)
        {
            return Results.NotFound();
        }

        product.Name = request.Name;
        product.Price = request.Price;

        await dbContext.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(
            CacheKey,
            cancellationToken);

        return Results.Ok(product);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        CatalogDbContext dbContext,
        IDistributedCache cache,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (product is null)
        {
            return Results.NotFound();
        }

        dbContext.Products.Remove(product);

        await dbContext.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(
            CacheKey,
            cancellationToken);

        return Results.NoContent();
    }
}

public sealed record CreateProductRequest(
    string Name,
    decimal Price);

public sealed record UpdateProductRequest(
    string Name,
    decimal Price);