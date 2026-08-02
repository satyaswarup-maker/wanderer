using wanderer_api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<GroqService>();
builder.Services.AddHttpClient<GeocodingService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("WandererPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://localhost:5174",
                "https://wanderer-web-omega.vercel.app" // Replace later with your actual Vercel URL
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Enable Swagger in ALL environments
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseCors("WandererPolicy");

app.UseAuthorization();

app.MapControllers();

app.Run();