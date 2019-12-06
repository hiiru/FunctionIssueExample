using FunctionApp2;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

[assembly: WebJobsStartup(typeof(Startup))]
namespace FunctionApp2
{
    public class Startup : IWebJobsStartup
    {
        public static string Environment;

        public void Configure(IWebJobsBuilder builder)
        {
            ReplaceConfig(builder);

            builder.Services.AddSingleton<Test>();
            builder.Services.AddSingleton<INameResolver, MyNameResolver>();
        }

        private void ReplaceConfig(IWebJobsBuilder builder)
        {
            var configServiceDescriptor = builder.Services.FirstOrDefault(d => d.ServiceType == typeof(IConfiguration));
            var baseConfig = configServiceDescriptor?.ImplementationInstance as IConfiguration;
            if (baseConfig == null)
            {
                var sp=builder.Services.BuildServiceProvider();
                baseConfig = sp.GetRequiredService<IConfiguration>();
            }
            Environment = baseConfig?.GetValue<string>("ASPNETCORE_ENVIRONMENT") ?? "Development";

            var config = new ConfigurationBuilder();
            if (baseConfig != null)
                config.AddConfiguration(baseConfig);

            config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{Environment}.json", optional: true, reloadOnChange: true);

            config.AddEnvironmentVariables();

            var newConfig = config.Build();
            var testOptions = newConfig.GetSection("Test");

            config.AddInMemoryCollection(new List<KeyValuePair<string, string>> {
                    new KeyValuePair<string, string>("Values:ProcessingStartQueue", testOptions.GetValue<string>("ProcessingStartQueue")),
                    new KeyValuePair<string, string>("ConnectionStrings:ProcessingQueueConnectionString", testOptions.GetValue<string>("ConnectionStringQueue"))
            });

            newConfig = config.Build();

            foreach(var service in builder.Services.ToList())
            {
                if (service.ServiceType == typeof(IConfiguration))
                    builder.Services.Remove(service);
            }
            
            builder.Services.Add(ServiceDescriptor.Singleton(typeof(IConfiguration), newConfig));
        }
    }
    public class MyNameResolver : INameResolver
    {
        private readonly string _queueName;

        public MyNameResolver(IConfiguration config)
        {
            _queueName = config?.GetSection("Test")?.GetValue<string>("ProcessingStartQueue");
        }

        public string Resolve(string name)
        {
            switch (name)
            {
                case "ProcessingStartQueue":
                    return _queueName;
            }
            return null;
        }
    }

    public class Test
    {
        private readonly DateTimeOffset _d;
        public Test()
        {
            _d = DateTimeOffset.Now;
        }

        public string A => _d.ToString();
    }
}
