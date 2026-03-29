// Program.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using note_app_be.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// JWT CONFIGURATION - SAMA DENGAN AUTH SERVICE
// ============================================
// Pastikan ini sama persis dengan Auth Service kamu!
var jwtKey = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT Key not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? "YourAuthService";
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? "YourClients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            // HARUS SAMA dengan Auth Service
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            ClockSkew = TimeSpan.Zero // Hilangkan tolerance untuk testing
        };

        // Event untuk debugging (optional)
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Auth failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var userId = context.Principal?.FindFirst("sub")?.Value;
                Console.WriteLine($"Token validated for user: {userId}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ============================================
// DATABASE - SQL SERVER + EF CORE
// ============================================
builder.Services.AddDbContext<NoteDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//CORS
builder.Services.AddCors(policyBuilder =>
    policyBuilder.AddDefaultPolicy(policy =>
        policy.WithOrigins("*").AllowAnyMethod().AllowAnyHeader()
    )
);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Authentication API",
        Version = "v1"
    });

    // JWT support in Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please Enter a valid token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            new string[] {}
        }
    });
});

var app = builder.Build();

// Middleware pipeline
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();

// WAJIB: Authentication sebelum Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Auto-migrate database (development only, production pakai proper migration)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NoteDbContext>();
    db.Database.Migrate();
}

app.Run();