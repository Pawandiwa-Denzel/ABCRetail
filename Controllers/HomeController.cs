using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Azure.Storage.Blobs;
using ABC_RetailApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ABC_RetailApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly string tableConnectionString;
        private readonly string tableName;
        private readonly string queueConnectionString;
        private readonly string queueName;
        private readonly string blobConnectionString;
        private readonly string blobContainerName;
        private readonly string fileBlobContainerName;

        // In-memory display storage
        private static List<CustomerProduct> TableData = new();
        private static List<UploadedFile> BlobData = new();
        private static List<string> QueueData = new();
        private static List<UploadedFile> FileData = new();

        public HomeController(IConfiguration config)
        {
            var azureConfig = config.GetSection("AzureStorage");
            tableConnectionString = azureConfig["TableConnectionString"];
            tableName = azureConfig["TableName"];
            queueConnectionString = azureConfig["QueueConnectionString"];
            queueName = azureConfig["QueueName"];
            blobConnectionString = azureConfig["BlobConnectionString"];
            blobContainerName = azureConfig["BlobContainerName"];
            fileBlobContainerName = azureConfig["FileContainerName"];
        }

        public async Task<IActionResult> Index()
        {
            // Load customers from Azure Table Storage
            try
            {
                var tableClient = new TableClient(tableConnectionString, tableName);
                TableData.Clear();
                await foreach (var entity in tableClient.QueryAsync<TableEntity>())
                {
                    TableData.Add(new CustomerProduct
                    {
                        Name = entity.GetString("Name"),
                        Product = entity.GetString("Product")
                    });
                }
            }
            catch { /* Handle connection errors */ }

            // Load Queue messages
            try
            {
                var queueClient = new QueueClient(queueConnectionString, queueName);
                var messages = await queueClient.ReceiveMessagesAsync(maxMessages: 32);
                QueueData.Clear();
                foreach (var msg in messages.Value)
                {
                    QueueData.Add(msg.MessageText);
                    await queueClient.DeleteMessageAsync(msg.MessageId, msg.PopReceipt);
                }
            }
            catch { }

            // Pass in-memory Blob/File data to view
            ViewBag.TableData = TableData;
            ViewBag.QueueData = QueueData;
            ViewBag.BlobData = BlobData;
            ViewBag.FileData = FileData;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddTable(string name, string product)
        {
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(product))
            {
                var tableClient = new TableClient(tableConnectionString, tableName);
                await tableClient.CreateIfNotExistsAsync();

                var entity = new TableEntity(Guid.NewGuid().ToString(), Guid.NewGuid().ToString())
                {
                    {"Name", name},
                    {"Product", product}
                };

                await tableClient.AddEntityAsync(entity);
                ViewBag.Result = "Customer & Product added to Azure Table Storage!";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> AddQueue(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                var queueClient = new QueueClient(queueConnectionString, queueName);
                await queueClient.CreateIfNotExistsAsync();
                await queueClient.SendMessageAsync(message);
                ViewBag.Result = "Order added to Azure Queue!";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> UploadBlob(IFormFile file)
        {
            if (file != null)
            {
                var blobClient = new BlobContainerClient(blobConnectionString, blobContainerName);
                await blobClient.CreateIfNotExistsAsync();
                var blob = blobClient.GetBlobClient(file.FileName);

                using var stream = file.OpenReadStream();
                await blob.UploadAsync(stream, overwrite: true);

                BlobData.Add(new UploadedFile
                {
                    FileName = file.FileName,
                    Url = blob.Uri.ToString(),
                    IsImage = true
                });

                ViewBag.Result = "Image uploaded to Azure Blob Storage!";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file != null)
            {
                var blobClient = new BlobContainerClient(blobConnectionString, fileBlobContainerName);
                await blobClient.CreateIfNotExistsAsync();
                var blob = blobClient.GetBlobClient(file.FileName);

                using var stream = file.OpenReadStream();
                await blob.UploadAsync(stream, overwrite: true);

                FileData.Add(new UploadedFile
                {
                    FileName = file.FileName,
                    Url = blob.Uri.ToString(),
                    IsImage = false
                });

                ViewBag.Result = "File uploaded to Azure Blob Storage!";
            }
            return RedirectToAction("Index");
        }
    }
}
