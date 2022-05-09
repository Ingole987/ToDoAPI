using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;

namespace ToDoAPI
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;

        public Function1(ILogger<Function1> log)
        {
            _logger = log;
        }

        //public async Task Methods()
        //{
        //    await this.DeleteTaskAsync(string id);
        //}

        [FunctionName("GetAllToDo")]
        [OpenApiOperation(operationId: "GetAllToDo", tags: new[] { "ToDoAPI" })]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> GetAllToDo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ToDoTasks")] HttpRequest req,
            [CosmosDB(
                databaseName: "ToDoAPIDB",
                collectionName: "ToDoListTable",
                ConnectionStringSetting = "CosmosDBConnection",
                SqlQuery = "SELECT * FROM c")]
                IEnumerable<ToDoItem> toDoItems)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            var toDoItems1 = new List<ToDoItem>();
            foreach (ToDoItem toDoItem in toDoItems)
            {
                _logger.LogInformation(toDoItem.Description);
                toDoItems1.Add(toDoItem);
            }
            return new OkObjectResult(toDoItems1);
        }

        [FunctionName("AddTODoItem")]
        [OpenApiOperation(operationId: "AddTODoItem", tags: new[] { "ToDoAPI" })]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ToDoItem), Required = true, Description = "New item details.")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> AddTODoItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ToDoTasks")] HttpRequest req,
             [CosmosDB(
                databaseName: "ToDoAPIDB",
                collectionName: "ToDoListTable",
                ConnectionStringSetting = "CosmosDBConnection")]
                IAsyncCollector<ToDoItem> toDoItemsOut)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject<ToDoItem>(requestBody);
            //var toDoItem = new ToDoItem()
            //{
            //    Id = Guid.NewGuid().ToString(),
            //    CreatedDate = DateTime.Now,
            //    Title = data.Title,
            //    Description = data.Description
            //};
            data.Id = Guid.NewGuid().ToString();
            data.CreatedDate = DateTime.Now;
            await toDoItemsOut.AddAsync(data);

            return new OkObjectResult(data);
        }

        [FunctionName("GetItemById")]
        [OpenApiOperation(operationId: "GetItemById", tags: new[] { "ToDoAPI" })]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ToDoItem), Description = "The OK response")]
        public async Task<IActionResult> GetItemById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ToDoTasks/{id}")] HttpRequest req,
            [CosmosDB(
                databaseName: "ToDoAPIDB",
                collectionName: "ToDoListTable",
                ConnectionStringSetting = "CosmosDBConnection",
                Id = "{id}",
                PartitionKey = "{id}")] ToDoItem toDoItem)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            if (toDoItem == null)
            {
                _logger.LogInformation($"ToDo item not found");
            }
            else
            {
                _logger.LogInformation($"Found ToDo item, Description={toDoItem.Description}");
            }
            return new OkObjectResult(toDoItem);
        }

        [FunctionName("UpdateTODoItem")]
        [OpenApiOperation(operationId: "UpdateTODoItem", tags: new[] { "ToDoAPI" })]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ToDoItem), Required = true, Description = "Updating Tasks.")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> UpdateTODoItem(
           [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ToDoTasks/{id}")] HttpRequest req,
            [CosmosDB(
                databaseName: "ToDoAPIDB",
                collectionName: "ToDoListTable",
                ConnectionStringSetting = "CosmosDBConnection"
                )
                ] DocumentClient documentClient , string id)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var updated = JsonConvert.DeserializeObject<ToDoItem>(requestBody);

            var collectionUri = UriFactory.CreateDocumentCollectionUri("ToDoAPIDB", "ToDoListTable");

            var document = documentClient.CreateDocumentQuery(collectionUri).Where(t => t.Id == id)
                  .AsEnumerable().FirstOrDefault();

            if (document == null)
            {
                return new NotFoundResult();
            }

            
            document.SetPropertyValue("Title", updated.Title);
            document.SetPropertyValue("Description", updated.Description);

            await documentClient.ReplaceDocumentAsync(document);

            return new OkObjectResult(document);

        }

        [FunctionName("DeleteItemById")]
        [OpenApiOperation(operationId: "DeleteItemById", tags: new[] { "ToDoList" })]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
        public async Task<IActionResult> DeleteItemById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "ToDoTasks/{id}")] HttpRequest req,
            [CosmosDB(
                databaseName: "ToDoAPIDB",
                collectionName: "ToDoListTable",
                ConnectionStringSetting = "CosmosDBConnection"
                )] DocumentClient documentClient, string id)
        {
            var collectionUri = UriFactory.CreateDocumentCollectionUri("ToDoAPIDB", "ToDoListTable");

            var document = documentClient.CreateDocumentQuery(collectionUri).Where(t => t.Id == id)
                  .AsEnumerable().FirstOrDefault();

            if (document == null)
            {
                return new NotFoundResult();
            }
            _ = await documentClient.DeleteDocumentAsync(document.SelfLink, new RequestOptions
            {
                PartitionKey = new Microsoft.Azure.Documents.PartitionKey(id)

            });
            string responseMessage = string.IsNullOrEmpty(id)
                ? "The Task Was Deleted."
                : $"Hello, {id}. This HTTP triggered function executed successfully.";



            return new OkResult();

        }
    }
}

