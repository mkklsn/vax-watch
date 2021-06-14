using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace vaccine_watcher
{
    public class watcher_func
    {
        private readonly IConfiguration _config;
        private CosmosClient _cosmosClient;

        private Database _database;
        private Container _container;

        public watcher_func(
            IConfiguration config,
            CosmosClient cosmosClient
            )
        {
            _config = config;
            _cosmosClient = cosmosClient;

            _database = cosmosClient
                .CreateDatabaseIfNotExistsAsync(Constants.COSMOS_DB_DATABASE_NAME)
                .GetAwaiter()
                .GetResult();

            var containerProps = new ContainerProperties(Constants.COSMOS_DB_CONTAINER_NAME, Constants.COSMOS_DB_PARTITION_KEY);
            _container = _database
                .CreateContainerIfNotExistsAsync(containerProps)
                .GetAwaiter()
                .GetResult();
        }
        
        [FunctionName("watcher_func")]
        [Timeout("00:00:15")]
        [return: TwilioSms(AccountSidSetting = "TwilioAccountSid", AuthTokenSetting = "TwilioAuthToken")]
        public async Task<CreateMessageOptions> RunAsync([TimerTrigger("0 0/30 * * * *", RunOnStartup = true)]TimerInfo myTimer, ILogger log)
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
                        var phoneNumberCountryCode = Environment.GetEnvironmentVariable("TwilioToPhoneNumberCountryCode", EnvironmentVariableTarget.Process);

                        await InsertUserAsync(phoneNumberTo, phoneNumberCountryCode, log);

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

                    await InsertUserAsync(Guid.NewGuid().ToString(), "+353", log);
                }
                return null;
            }
        }
    
        private async Task<bool> DoesUserExistsAsync(string id, string partitionKey)
        {
            var userResponse = await _container.ReadItemAsync<User>(id, new PartitionKey(partitionKey));

            return userResponse.StatusCode == HttpStatusCode.NotFound;
        }

        private async Task InsertUserAsync(string phoneNumberTo, string phoneNumberCountryCode, ILogger log)
        {
            var user = new User
            {
                Id = phoneNumberTo,
                CountryCode = phoneNumberCountryCode,
                PhoneNumber = phoneNumberTo,
                CreatedDateTimeUtc = DateTime.UtcNow
            };

            if (await DoesUserExistsAsync(user.Id, user.CountryCode))
            {
                var msg = string.Format("Item in database with id: {0} already exists\n", user.Id);
                log.LogInformation(msg);
                throw new Exception(msg);
            }

            try
            {
                var userCreateResponse = await _container.CreateItemAsync<User>(user, new PartitionKey(user.CountryCode));
                log.LogInformation("Created item in database with id: {0} Operation consumed {1} RUs.\n", userCreateResponse.Resource.Id, userCreateResponse.RequestCharge);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                log.LogInformation("Item in database with id: {0} already exists\n", user.Id);
                throw;
            }
            catch (CosmosException ex) 
            {
                log.LogError(ex, "Failed to insert user.");
                throw;
            }
        }
    }
}
