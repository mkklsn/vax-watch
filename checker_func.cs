using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace vaccine_watcher
{
    public static class checker_func
    {
        [FunctionName("checker_func")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string fname = req.Query["fname"];
            string lname = req.Query["lname"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            fname = fname ?? data?.fname;
            lname = lname ?? data?.lname;

            string responseMessage = string.IsNullOrEmpty(fname) || string.IsNullOrEmpty(fname)
                ? "This HTTP triggered function executed successfully. Pass a fname or lname in the query strings or in the request body for a personalized response."
                : $"Hello, {fname} {lname}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }
    }
}
