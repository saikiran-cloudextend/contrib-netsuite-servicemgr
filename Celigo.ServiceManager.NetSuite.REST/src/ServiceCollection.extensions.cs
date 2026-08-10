using System;
using System.Linq;
using System.Net.Http;
using System.Xml.Schema;
using Celigo.NetSuite.ConnectionGuard.Abstractions;
using Celigo.NetSuite.ConnectionGuard.Decorators;
using Celigo.ServiceManager.NetSuite.REST;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    using Options = Microsoft.Extensions.Options.Options;
    
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddNetSuiteRestClientSupport(this IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddOptions()
                .Configure<RestClientOptions>(configuration.GetSection(RestClientOptions.ConfigurationSectionName))
                .AddHttpClient()
                .AddHttpClient<RestClient>();

            services.AddNetSuiteConnectionGuard(configuration);
            services.AddTransient<IRestClient>(sp => new GuardedRestClient(
                sp.GetRequiredService<RestClient>(),
                sp.GetRequiredService<GuardPipeline>(),
                sp.GetRequiredService<INsCallContextAccessor>()));

            return services;
        }

        public static IServiceCollection AddRestletClient(this IServiceCollection services,
                                                            IConfiguration configuration, 
                                                            params RestletConfig[] restlets)
        {
            services
                .AddNetSuiteRestClientSupport(configuration)
                .Configure<RestletConfig>(configuration.GetSection(RestletConfig.ConfigurationSectionName))
                .Configure<RestletConfigOptions>(configuration.GetSection(RestletConfig.ConfigurationSectionName));

            if (restlets.Length > 0)
            {
                var restletConfigOptions = new RestletConfigOptions { Restlets = restlets };
                if (restlets.Any(r => string.IsNullOrEmpty(r.RestletName)))
                {
                    throw new ArgumentNullException($"{nameof(RestletConfig)}.{nameof(RestletConfig.RestletName)}");
                }
                services.AddSingleton(Options.Create(restletConfigOptions));
            }

            services.AddHttpClient<IRestletClient, RestletClient>();

            services.AddSingleton<IRestletClientFactory, RestletClientFactory>();

            return services;
        }

        public static IServiceCollection AddGuardedRestletClient(this IServiceCollection services,
                                                                 IConfiguration configuration,
                                                                 params RestletConfig[] restlets)
        {
            services
                .AddNetSuiteRestClientSupport(configuration)
                .Configure<RestletConfig>(configuration.GetSection(RestletConfig.ConfigurationSectionName))
                .Configure<RestletConfigOptions>(configuration.GetSection(RestletConfig.ConfigurationSectionName));

            if (restlets.Length > 0)
            {
                var restletConfigOptions = new RestletConfigOptions { Restlets = restlets };
                if (restlets.Any(r => string.IsNullOrEmpty(r.RestletName)))
                {
                    throw new ArgumentNullException($"{nameof(RestletConfig)}.{nameof(RestletConfig.RestletName)}");
                }
                services.AddSingleton(Options.Create(restletConfigOptions));
            }

            services.AddHttpClient<RestletClient>();
            services.AddNetSuiteConnectionGuard(configuration);
            services.AddTransient<IRestletClient>(sp => new GuardedRestletClient(
                sp.GetRequiredService<RestletClient>(),
                sp.GetRequiredService<GuardPipeline>(),
                sp.GetRequiredService<INsCallContextAccessor>()));

            services.AddSingleton<RestletClientFactory>();
            services.AddSingleton<IRestletClientFactory>(sp => new GuardedRestletClientFactory(
                sp.GetRequiredService<RestletClientFactory>(),
                sp.GetRequiredService<GuardPipeline>(),
                sp.GetRequiredService<INsCallContextAccessor>()));

            return services;
        }
    }
}