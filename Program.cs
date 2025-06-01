using KuwagoAPI.Helper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Diagnostics;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var startup = new KuwagoAPI.Startup(builder.Configuration);
startup.ConfigureServices(builder.Services);

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
        ClockSkew = TimeSpan.Zero,
        ValidateIssuerSigningKey = true,
        ValidIssuer = "KuwagoAPI",
        ValidAudience = "KuwagoClient",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a-string-secret-at-least-256-bits-long"))
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception is SecurityTokenExpiredException)
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                var result = System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    statusCode = 401,
                    message = "Token has expired."
                });
                return context.Response.WriteAsync(result);
            }

            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            var genericResult = System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                statusCode = 401,
                message = "Invalid token."
            });
            return context.Response.WriteAsync(genericResult);
        },

        OnTokenValidated = context =>
        {
            Debug.WriteLine("Token successfully validated.");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "KuwagoAPI", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\nEnter 'Bearer' followed by your token in the text input below.\r\n\r\nExample: \"Bearer abc123\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
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

    c.OperationFilter<AddUnauthorizedResponseOperationFilter>();
});


var app = builder.Build();

startup.Configure(app, app.Environment); // Keep this if it's configuring services only

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseSession();  
app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers(); 

app.Run();

