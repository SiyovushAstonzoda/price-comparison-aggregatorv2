using Dapper;
using Microsoft.Data.SqlClient;
using PriceAggregator.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Allow your frontend (running on a different port/file) to call this API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowFrontend");

string connectionString = "Server=127.0.0.1;Database=Aggregator;User Id=sa;Password=Siyovush_2026!;TrustServerCertificate=True;";

app.MapGet("/api/products", async () =>
{
    using var db = new SqlConnection(connectionString);
    var products = await db.QueryAsync<ProductDto>(
        "SELECT Source, Title, ImageUrl, Price, RegularPrice, ProductUrl FROM Products ORDER BY LastUpdated DESC");
    return Results.Ok(products);
});

app.Run();