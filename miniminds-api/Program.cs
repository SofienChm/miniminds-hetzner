using DaycareAPI.Data;
using DaycareAPI.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Add JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!))
    };
    
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/notificationHub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// Add CORS
builder.Services.AddCors(options =>
{
    // Production policy - strict origin whitelist
    options.AddPolicy("ProductionCors",
        policy =>
        {
            policy.WithOrigins(
                    "https://app-miniminds.com",           // Production domain (update this)
                    "https://www.app-miniminds.com",       // Production www subdomain
                    "capacitor://localhost",           // iOS Capacitor
                    "http://localhost"                 // Android Capacitor
                  )
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });

    // Development policy - permissive for local testing
    options.AddPolicy("DevelopmentCors",
        policy =>
        {
            policy.SetIsOriginAllowed(origin => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Add HttpClient for AI services
builder.Services.AddHttpClient();

// Add SignalR
builder.Services.AddSignalR();

// Add Push Notification Service (must be registered before NotificationService)
builder.Services.AddScoped<DaycareAPI.Services.IPushNotificationService, DaycareAPI.Services.PushNotificationService>();

// Add Notification Service
builder.Services.AddScoped<DaycareAPI.Services.INotificationService, DaycareAPI.Services.NotificationService>();
builder.Services.AddScoped<DaycareAPI.Services.NotificationService>();

// Add Email Service
builder.Services.AddScoped<DaycareAPI.Services.IEmailService, DaycareAPI.Services.EmailService>();

// Add Geofence Service
builder.Services.AddScoped<DaycareAPI.Services.IGeofenceService, DaycareAPI.Services.GeofenceService>();

// Add Holiday Service
builder.Services.AddScoped<DaycareAPI.Services.IHolidayService, DaycareAPI.Services.HolidayService>();

// Configure Stripe
Stripe.StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Daycare API",
        Version = "v1",
        Description = "API pour la gestion de crèche"
    });
    
    // Configuration pour l'authentification JWT
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();

// Seed the database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Apply pending migrations
        await context.Database.MigrateAsync();
        Console.WriteLine("Database connection established.");

        // Seed the database with sample data
        await DatabaseSeeder.SeedAsync(context, userManager, roleManager);
        Console.WriteLine("Database seeded successfully.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure the HTTP request pipeline.
// Activer Swagger en développement et production pour les tests
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Daycare API V1");
    c.RoutePrefix = "swagger"; // Accessible via /swagger
    c.DocumentTitle = "Daycare API Documentation";
});

// Use environment-appropriate CORS policy
if (app.Environment.IsDevelopment())
{
    app.UseCors("DevelopmentCors");
    Console.WriteLine("CORS: Using permissive development policy");
}
else
{
    app.UseCors("ProductionCors");
    Console.WriteLine("CORS: Using strict production policy");
}

// Serve static files
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<DaycareAPI.Hubs.NotificationHub>("/notificationHub");

// Configure URL
app.Urls.Add("http://0.0.0.0:5001");
Console.WriteLine("Server running at: http://0.0.0.0:5001");

app.Run();