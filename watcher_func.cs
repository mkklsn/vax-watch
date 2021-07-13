using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
        [Timeout("00:00:05")]
        [return: TwilioSms(AccountSidSetting = "TwilioAccountSid", AuthTokenSetting = "TwilioAuthToken")]
        public async Task<CreateMessageOptions> RunAsync(
            [TimerTrigger("*/10 * * * *")]TimerInfo myTimer,
            ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            log.LogInformation($"Next schedule: {myTimer.ScheduleStatus.Next}");

            var phoneNumberFrom = Environment.GetEnvironmentVariable("TwilioFromPhoneNumber", EnvironmentVariableTarget.Process);
            var phoneNumberTo = Environment.GetEnvironmentVariable("TwilioToPhoneNumber", EnvironmentVariableTarget.Process);
            var phoneNumberCountryCode = Environment.GetEnvironmentVariable("TwilioToPhoneNumberCountryCode", EnvironmentVariableTarget.Process);
            if (await DoesUserExistsAsync(phoneNumberTo, phoneNumberCountryCode, log))
            {
                var exMsg = string.Format("User in database with id: {0} already exists\n", phoneNumberTo);
                log.LogInformation(exMsg);
                throw new Exception(exMsg);
            }

            var siteContent = string.Empty;
            using (var httpClient = new HttpClient())
            {
                siteContent = await GetWebsiteContent(httpClient, "https://vaccine.hse.ie/cohort/", log);
            }

            var has30s = siteContent.Contains("30 to 69", StringComparison.OrdinalIgnoreCase);
            if (has30s)
            {
                log.LogInformation("Vaccine is not yet available for 25+.");
                return null;
            }

            await InsertUserAsync(phoneNumberTo, phoneNumberCountryCode, log);

            var msg = "Vaccine is now available for 25+!";
            log.LogInformation(msg);

            var sms = new CreateMessageOptions(new PhoneNumber(phoneNumberTo))
            {
                From = new PhoneNumber(phoneNumberFrom),
                Body = msg
            };

            return sms;
        }

        private async Task<string> GetWebsiteContent(HttpClient httpClient, string url, ILogger log)
        {
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                log.LogError($"Status code is not success => code: {response.StatusCode}");
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }
    
        private async Task<bool> DoesUserExistsAsync(string id, string partitionKey, ILogger log)
        {
            ItemResponse<User> userResponse = null;
            try 
            {
                userResponse = await _container.ReadItemAsync<User>(id, new PartitionKey(partitionKey));
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                log.LogInformation("User {id} does not exist.", id);
                log.LogInformation("Operation consumed {charge} RUs.", ex.RequestCharge);
                return false;
            }
            catch (CosmosException ex) 
            {
                log.LogError(ex, $"Failed to read user. Status code: {ex.StatusCode} | {ex.Message}");
                log.LogInformation("Operation consumed {charge} RUs.", ex.RequestCharge);
                throw;
            }

            log.LogInformation("Response => {response}", JsonConvert.SerializeObject(userResponse));
            log.LogInformation($"Response status code => {userResponse.StatusCode}");
            log.LogInformation("Operation consumed {charge} RUs.", userResponse.RequestCharge);

            return userResponse.StatusCode == HttpStatusCode.OK;
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

            try
            {
                var userCreateResponse = await _container.CreateItemAsync<User>(user, new PartitionKey(user.CountryCode));
                log.LogInformation("Created item in database with id: {0} Operation consumed {1} RUs.\n", userCreateResponse.Resource.Id, userCreateResponse.RequestCharge);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                log.LogInformation("Item in database with id: {0} already exists\n", user.Id);
                log.LogInformation("Operation consumed {charge} RUs.", ex.RequestCharge);
                throw;
            }
            catch (CosmosException ex) 
            {
                log.LogError(ex, "Failed to insert user.");
                log.LogInformation("Operation consumed {charge} RUs.", ex.RequestCharge);
                throw;
            }
        }
    }
}
