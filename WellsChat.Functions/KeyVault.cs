using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using System.Text.Json;
using Azure.Security.KeyVault.Secrets;

namespace WellsChat.Functions
{
    public static class KeyVault
    {
        [FunctionName("KeyVault")]
        public static async Task<IActionResult> Run(
                       [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req)
        {
            var key = req.Query["key"];
            var secretClient = new SecretClient(new Uri(Environment.GetEnvironmentVariable("VaultUri")), new DefaultAzureCredential());
            var secret = await secretClient.GetSecretAsync(key);
            var secretValue = secret.Value.Value;
            return new OkObjectResult(secretValue);
        }
    }
}
