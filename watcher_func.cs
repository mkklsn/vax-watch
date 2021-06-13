using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace vaccine_watcher
{
    public static class watcher_func
    {
        [FunctionName("watcher_func")]
        [Timeout("00:00:15")]
        [return: TwilioSms(AccountSidSetting = "TwilioAccountSid", AuthTokenSetting = "TwilioAuthToken")]
        public static async Task<CreateMessageOptions> RunAsync([TimerTrigger("0 0/05 * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            log.LogInformation($"Next schedule: {myTimer.ScheduleStatus.Next}");

            using (var httpClient = new HttpClient()) 
            {
                var response = await httpClient.GetAsync("https://vaccine.hse.ie/cohort/");

                if (!response.IsSuccessStatusCode)
                {
                    log.LogError($"Status code is not success => code: {response.StatusCode}");
                    return null;
                }

                var siteContent = await response.Content.ReadAsStringAsync();

                var msg = string.Empty;
                if (siteContent.Contains("30 to 69", StringComparison.OrdinalIgnoreCase))
                {
                    msg = "Vaccine is now available for 30s and above!";
                    
                    log.LogInformation(msg);

                    try
                    {
                        var phoneNumberFrom = Environment.GetEnvironmentVariable("TwilioFromPhoneNumber", EnvironmentVariableTarget.Process);
                        var phoneNumberTo = Environment.GetEnvironmentVariable("TwilioToPhoneNumber", EnvironmentVariableTarget.Process);
                        var message = new CreateMessageOptions(new PhoneNumber(phoneNumberTo))
                        {
                            From = new PhoneNumber(phoneNumberFrom),
                            Body = msg
                        };

                        return message;
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, "Exception occurred when sending sms.");
                    }
                }
                else
                {
                    msg = "Vaccine is not yet available for 30s.";
                    log.LogInformation(msg);
                }
                return null;
            }
        }
    }
}
