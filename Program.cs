using KuwagoAPI.Helper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Reflection;

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
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";

            string message = context.Exception is SecurityTokenExpiredException
                ? "Token has expired."
                : "Invalid token.";

            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                statusCode = 401,
                message = message
            });

            return context.Response.WriteAsync(result);
        },
        OnChallenge = context =>
        {
            if (!context.Response.HasStarted)
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";

                var result = System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    statusCode = 401,
                    message = "Token is missing or not authorized."
                });

                return context.Response.WriteAsync(result);
            }

            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Debug.WriteLine("Token successfully validated.");
            return Task.CompletedTask;
        },
        OnForbidden = context =>
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";

            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                statusCode = 403,
                message = "You do not have permission to access this resource."
            });

            return context.Response.WriteAsync(result);
        }
    };

});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("All", policy =>
        policy.RequireAssertion(context =>
        {
            var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;
            return roleClaim == "0"|| roleClaim == "1"|| roleClaim == "2";
        }));

    options.AddPolicy("AdminOnly", policy =>
       policy.RequireAssertion(context =>
       {
           var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;
           return roleClaim == "0";
       }));

    options.AddPolicy("BorrowerOnly", policy =>
     policy.RequireAssertion(context =>
     {
         var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;
         return roleClaim == "2";
     }));
    options.AddPolicy("LenderOnly", policy =>
   policy.RequireAssertion(context =>
   {
       var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;
       return roleClaim == "1";
   }));
    options.AddPolicy("AdminLender", policy =>
  policy.RequireAssertion(context =>
  {
      var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;
      return roleClaim == "0" || roleClaim == "1";
  }));
    options.AddPolicy("LenderBorrower", policy =>
policy.RequireAssertion(context =>
{
    var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;
    return roleClaim == "1" || roleClaim == "2";
}));
});


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "KuwagoAPI", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme...",
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

    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
});



var app = builder.Build();

startup.Configure(app, app.Environment);

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

