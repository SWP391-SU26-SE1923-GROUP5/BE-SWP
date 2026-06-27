using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AIStudyHub.API.Swagger;

public class FormFileOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var actionDescriptor = context.ApiDescription.ActionDescriptor as ControllerActionDescriptor;
        if (actionDescriptor == null)
            return;

        var hasFormFileDto = actionDescriptor.MethodInfo
            .GetParameters()
            .Any(p => p.ParameterType.Name == "UploadDocumentFileRequestDto");

        if (!hasFormFileDto)
            return;

        // Prevent Swashbuckle from auto-generating a broken schema for IFormFile
        operation.Parameters = new List<OpenApiParameter>();
        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["file"] = new OpenApiSchema
                            {
                                Type = "string",
                                Format = "binary",
                                Description = "File to upload (.pdf, .docx, .txt, .md, .jpg, .png, .mp4, .mp3, etc.)"
                            },
                            ["title"] = new OpenApiSchema
                            {
                                Type = "string",
                                Description = "Document title"
                            },
                            ["subjectId"] = new OpenApiSchema
                            {
                                Type = "string",
                                Format = "uuid",
                                Description = "Subject ID"
                            }
                        },
                        Required = new HashSet<string> { "file", "title", "subjectId" }
                    }
                }
            }
        };
    }
}
