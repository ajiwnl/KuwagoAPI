using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;

namespace KuwagoAPI.Helper
{
    public class AddUnauthorizedResponseOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Add 401 response if not already present
            if (!operation.Responses.ContainsKey("401"))
            {
                operation.Responses.Add("401", new OpenApiResponse
                {
                    Description = "Unauthorized - Token expired or invalid",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = context.SchemaGenerator.GenerateSchema(typeof(StatusResponse), context.SchemaRepository)
                        }
                    }
                });

            }
        }
    }
}
