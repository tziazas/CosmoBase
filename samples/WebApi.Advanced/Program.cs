using CosmoBase.DependencyInjection;
using CosmoBase.Abstractions.Interfaces;
using WebApi.Advanced.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using WebApi.Advanced.Endpoints;

namespace WebApi.Advanced;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        ConfigureServices(builder);

        var app = builder.Build();
        
        await EnsureDatabaseExistsAsync(app.Services);

        // Configure the HTTP request pipeline
        ConfigurePipeline(app);

        app.Run();
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        // Add basic services
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "CosmoBase Advanced Web API",
                Version = "v1",
                Description =
                    "Advanced Web API demonstrating CosmoBase features with authentication, validation, and comprehensive CRUD operations."
            });

            // Add JWT Authentication to Swagger
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description =
                    "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        // Add HTTP context accessor for user context
        builder.Services.AddHttpContextAccessor();

        // Add authentication (simplified for demo)
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Configure JWT validation (simplified for demo)
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = false,
                    RequireExpirationTime = false,
                    ClockSkew = TimeSpan.Zero
                };
            });

        builder.Services.AddAuthorization();

        // Custom user context for web applications
        builder.Services.AddSingleton<IUserContext, WebUserContext>();
        
        builder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("localsettings.json", optional: true, reloadOnChange: true);

        // Add CosmoBase with web user context
        builder.Services.AddCosmoBase(
            builder.Configuration,
            builder.Services.BuildServiceProvider().GetRequiredService<IUserContext>(),
            config =>
            {
                // Log configuration for demo purposes
                Console.WriteLine($"✅ CosmoBase configured with {config.CosmosClientConfigurations.Count} clients");
            });

        // Add application services
        builder.Services.AddScoped<IProductService, ProductService>();
        builder.Services.AddScoped<IOrderService, OrderService>();
        builder.Services.AddScoped<IInventoryService, InventoryService>();

        // Add health checks
        builder.Services.AddHealthChecks()
            .AddCheck<CosmosHealthCheck>("cosmos-db");

        // Add CORS for frontend applications
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });
    }

    private static void ConfigurePipeline(WebApplication app)
    {
        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "CosmoBase Advanced Web API v1");
                c.RoutePrefix = string.Empty; // Serve Swagger UI at the app's root
            });
        }

        app.UseHttpsRedirection();
        app.UseCors("AllowFrontend");
        app.UseAuthentication();
        app.UseAuthorization();

        // Add global exception handling
        app.UseMiddleware<GlobalExceptionMiddleware>();

        app.MapControllers();
        app.MapHealthChecks("/health");

        // Add some demo endpoints
        app.MapGet("/api/demo/user", [Authorize](HttpContext context) =>
        {
            var userClaims = context.User.Claims.Select(c => new { c.Type, c.Value });
            return Results.Ok(new { User = context.User.Identity?.Name, Claims = userClaims });
        });

        app.MapOrderEndpoints();
        app.MapProductEndpoints();

        Console.WriteLine("🚀 CosmoBase Advanced Web API is running!");
        Console.WriteLine("📖 Swagger UI available at: https://localhost:5001");
        Console.WriteLine("🏥 Health checks available at: https://localhost:5001/health");
    }
    
    private static async Task EnsureDatabaseExistsAsync(IServiceProvider services)
    {
        Console.WriteLine("🔧 Setting up database and containers...");
    
        var cosmosClients = services.GetRequiredService<IReadOnlyDictionary<string, Microsoft.Azure.Cosmos.CosmosClient>>();
        var config = services.GetRequiredService<CosmoBase.Abstractions.Configuration.CosmosConfiguration>();
    
        var client = cosmosClients.Values.First();
    
        // Create database
        var database = await client.CreateDatabaseIfNotExistsAsync("SampleAppDb");
        Console.WriteLine("✅ Database 'SampleAppDb' ready");
    
        // Create containers based on your configuration
        foreach (var modelConfig in config.CosmosModelConfigurations)
        {
            var partitionKeyPath = $"/{modelConfig.PartitionKey}";
            await database.Database.CreateContainerIfNotExistsAsync(
                modelConfig.CollectionName,
                partitionKeyPath);
        
            Console.WriteLine($"✅ Container '{modelConfig.CollectionName}' ready (partition: {partitionKeyPath})");
        }
    }
}

/// <summary>
/// Web-specific user context that extracts user information from HTTP context
/// </summary>
public class WebUserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    public string GetCurrentUser()
    {
        var context = httpContextAccessor.HttpContext;

        if (context?.User.Identity?.IsAuthenticated == true)
        {
            // Try to get user ID from various claim types
            return context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? context.User.FindFirst("sub")?.Value
                   ?? context.User.FindFirst(ClaimTypes.Name)?.Value
                   ?? context.User.Identity.Name
                   ?? "AuthenticatedUser";
        }

        // For demo purposes, return a default user when not authenticated
        return "AnonymousUser";
    }
}

/// <summary>
/// Global exception handling middleware
/// </summary>
public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = exception switch
        {
            ArgumentException => 400,
            UnauthorizedAccessException => 401,
            KeyNotFoundException => 404,
            _ => 500
        };

        var response = new
        {
            error = new
            {
                message = exception.Message,
                type = exception.GetType().Name,
                statusCode = context.Response.StatusCode
            }
        };

        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
}