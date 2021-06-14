using System.Linq;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(vaccine_watcher.Startup))]

namespace vaccine_watcher
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var config = builder.GetContext().Configuration;

            builder.Services.AddSingleton((s) =>
            {
                var cosmosClientBuilder = new CosmosClientBuilder(config[Constants.COSMOS_DB_CONNECTION_STRING]);

                return cosmosClientBuilder.WithConnectionModeDirect()
                    .WithApplicationRegion("North Europe")
                    .WithBulkExecution(true)
                    .Build();
            });
        }
    }
}