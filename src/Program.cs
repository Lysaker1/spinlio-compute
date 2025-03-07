/**
 * 🚀 Spinlio Compute - Main Application Entry Point
 * 
 * This file configures and starts the ASP.NET Core web application that serves
 * as a Rhino Compute middleware for 3D model conversion and parametric design.
 * 
 * The application connects to:
 * - AWS S3 for storage
 * - Rhino Compute for geometry processing
 * - Serilog for structured logging
 */

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Amazon.S3;
using SpinlioCompute.Services;
using SpinlioCompute.Utils;
using Rhino.Compute;
using Serilog;

// Create the web application builder
var builder = WebApplication.CreateBuilder(args);

// 📝 Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console() // Log to console for real-time visibility
    .WriteTo.File("logs/spinlio-compute-.log", rollingInterval: RollingInterval.Day) // Daily log files
    .CreateLogger();

// Set Serilog as the logging provider
builder.Host.UseSerilog();

// 🧩 Register framework services
builder.Services.AddControllers(); // MVC Controllers for API endpoints
builder.Services.AddEndpointsApiExplorer(); // API explorer for documentation
builder.Services.AddSwaggerGen(); // Swagger for API documentation

// 📦 Configure AWS services
builder.Services.AddAWSService<IAmazonS3>(); // S3 client for storage operations

// 🔄 Configure CORS to allow cross-origin requests
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        policy =>
        {
            policy.WithOrigins("*") // Allow requests from any origin
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// 🧰 Register application services
builder.Services.AddSingleton<ModelConversionService>(); // Service for model conversion
builder.Services.AddSingleton<GrasshopperService>(); // Service for Grasshopper operations

// 🦏 Configure Rhino Compute connection
string computeUrl = builder.Configuration["RhinoCompute:ServerUrl"] ?? "http://localhost:6500/";
string apiKey = builder.Configuration["RhinoCompute:ApiKey"] ?? "";
ComputeServer.WebAddress = computeUrl; // Set the URL to the Rhino Compute server
ComputeServer.ApiKey = apiKey; // Set the API key for authentication

// Add explicit logging service
builder.Services.AddLogging();

// 🏗️ Build the application
var app = builder.Build();

// 🔧 Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    // Enable Swagger UI in development
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection(); // Redirect HTTP to HTTPS
app.UseCors(); // Enable CORS
app.UseAuthorization(); // Enable authorization
app.MapControllers(); // Map controller endpoints

app.MapPost("/convert", async (HttpContext context, [FromBody] ConversionRequest request) => {
    // Forward to the controller
    var controller = context.RequestServices.GetRequiredService<ConvertController>();
    return await controller.InitiateConversion(request);
});

// 🚀 Ensure services are initialized at startup
app.Services.GetRequiredService<ModelConversionService>();
app.Services.GetRequiredService<GrasshopperService>();

// ✨ Start the application with proper error handling
try
{
    Log.Information("Starting Spinlio Compute server");
    app.Run(); // Start the web server
}
catch (Exception ex)
{
    // Log any fatal errors during startup
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    // Ensure logs are flushed on shutdown
    Log.CloseAndFlush();
} 