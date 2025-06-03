var builder = WebApplication.CreateBuilder(args);

// Register HttpClient and controllers
builder.Services.AddHttpClient();
builder.Services.AddControllers(); // Add this

// Optional: Add Swagger for testing
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Use Swagger in dev
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers(); // Map your ChatProxyController

app.Run();
