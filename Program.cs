using AssetHub.Api.Authorization;
using AssetHub.Api.Data;
using AssetHub.Api.Infrastructure;
using AssetHub.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<AuditService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularPolicy", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:4200",
                "http://localhost:4300",
                "http://localhost",
                "http://10.100.1.82",
                "http://10.100.1.82:80",
                "http://10.100.1.82:4200",
                "http://10.100.1.82:4300"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is missing or shorter than 32 characters. Set it in appsettings.Development.json " +
        "(see appsettings.Development.example.json) or the Jwt__Key environment variable.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            )
        };

        // Reject tokens of users that were deleted, deactivated or had their role changed
        // after the token was issued.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal!;

                if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                {
                    context.Fail("Token has no user id");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var user = await db.SystemUsers
                    .AsNoTracking()
                    .Where(x => x.Id == userId)
                    .Select(x => new { x.IsActive, x.Role })
                    .FirstOrDefaultAsync();

                if (user == null || !user.IsActive)
                {
                    context.Fail("User no longer exists or is inactive");
                    return;
                }

                if (user.Role != principal.FindFirstValue(ClaimTypes.Role))
                    context.Fail("User role has changed; sign in again");
            }
        };
    });

// GET endpoints only need [Authorize] (any authenticated user).
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.RequireSuperAdmin, policy => policy.RequireRole(Roles.SuperAdmin));
    options.AddPolicy(Policies.CanEdit, policy => policy.RequireRole(Roles.SuperAdmin, Roles.Admin));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Paste the token from POST /api/Auth/login (without the \"Bearer \" prefix).",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = JwtBearerDefaults.AuthenticationScheme }
    };

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

var uploadsRoot = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "uploads");
Directory.CreateDirectory(uploadsRoot);

app.UseSwagger();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("./v1/swagger.json", "AssetHub API v1");
    options.RoutePrefix = "swagger";
});

app.UseCors("AngularPolicy");

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await SuperAdminSeeder.SeedAsync(app.Services);

app.Run();

// Lets the integration tests host the API with WebApplicationFactory<Program>.
public partial class Program { }
