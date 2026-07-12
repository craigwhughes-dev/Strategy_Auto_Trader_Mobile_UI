using MobileUI.Api.Endpoints;
using MobileUI.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IStatusReader, StatusReader>();
builder.Services.AddScoped<IJournalReader, JournalReader>();
builder.Services.AddHttpClient<IPriceFetcher, PriceFetcher>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalNetwork", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowLocalNetwork");

app.MapPositionsEndpoints();

app.Run();
