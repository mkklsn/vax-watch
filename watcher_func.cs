using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace vaccine_watcher
{
    public static class watcher_func
    {
        [FunctionName("watcher_func")]
        [Timeout("00:00:15")]
        public static async Task RunAsync([TimerTrigger("0 0/15 * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            log.LogInformation($"Next schedule: {myTimer.ScheduleStatus.Next}");

            using (var httpClient = new HttpClient()) 
            {
                var response = await httpClient.GetAsync("https://vaccine.hse.ie/cohort/");

                if (!response.IsSuccessStatusCode)
                {
                    log.LogError($"Status code is not success => code: {response.StatusCode}");
                    return;
                }

                var siteContent = await response.Content.ReadAsStringAsync();

                if (siteContent.Contains("30 to 69", StringComparison.OrdinalIgnoreCase))
                {
                    log.LogInformation("Vaccine is now available for 30s and above!");
                }
                else
                {
                    log.LogInformation("Vaccine is not yet available for 30s.");
                }
            }
        }
    }
}
