using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Resend;
using CasaCambio.Server.Auth;
using CasaCambio.Server.Data;
using CasaCambio.Server.Middleware;
using CasaCambio.Server.Services;
using CasaCambio.Server.Validators;

var builder = WebApplication.CreateBuilder(args);

// Database (PgBouncer transaction mode: disable prepared statements)
var connString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(connString, npgsqlOptions =>
        npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)));

// JWT
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
builder.Services.Configure<JwtSettings>(jwtSettings);
builder.Services.AddSingleton<JwtService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!))
        };
    });
builder.Services.AddAuthorization();

// Business Services
builder.Services.AddSingleton<IAuditService, AuditService>();
builder.Services.AddSingleton<ICierreCajaService, CierreCajaService>();
builder.Services.AddSingleton<IOperacionService, OperacionService>();
builder.Services.AddSingleton<IPPPService, PPPService>();
builder.Services.AddSingleton<IArqueoService, ArqueoService>();
builder.Services.AddSingleton<OperacionValidator>();
builder.Services.AddSingleton<ArqueoValidator>();

builder.Services.AddOptions();
builder.Services.AddHttpClient<ResendClient>();
builder.Services.Configure<ResendClientOptions>(o =>
    o.ApiToken = builder.Configuration["Email:Password"]!);
builder.Services.AddTransient<IResend, ResendClient>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Casa de Cambio API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed admin user (skip gracefully if DB not reachable)
try
{
    using var scope = app.Services.CreateScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using var db = factory.CreateDbContext();
    db.Database.EnsureCreated();
    if (!db.Usuarios.Any())
    {
        db.Usuarios.Add(new CasaCambio.Server.Models.Usuario
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            NombreCompleto = "Administrador",
            Rol = "Admin"
        });
        db.SaveChanges();
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning("No se pudo conectar a la base de datos al iniciar: {Message}. El servidor arrancara igualmente.", ex.Message);
}

// Middleware
app.UseMiddleware<ExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
