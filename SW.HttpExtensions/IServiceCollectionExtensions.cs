using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;

namespace SW.HttpExtensions
{
    public static class IServiceCollectionExtensions
    {
        // IHttpClientFactory pools connections inside each SocketsHttpHandler indefinitely by
        // default (PooledConnectionLifetime is Timeout.InfiniteTimeSpan) - a connection is only
        // ever recycled if the *handler* itself gets rotated (HttpClientFactory does that every 2
        // minutes by default, but a handler with in-flight or recently-used connections can live
        // far longer than that). A connection that goes stale server-side - the remote pod
        // restarting, a NAT/conntrack entry expiring, a load balancer dropping an idle socket -
        // fails silently on its next reuse instead of erroring at the point the underlying
        // network dropped it. Bounding PooledConnectionLifetime forces a fresh connection (and
        // fresh DNS resolution) at least this often, so a connection can never go stale for
        // longer than this window before it is discarded and replaced.
        private static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(2);

        private static void ConfigureHandler(IHttpClientBuilder builder) =>
            builder.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = PooledConnectionLifetime
            });

        public static IServiceCollection AddJwtTokenParameters(this IServiceCollection serviceCollection, Action<JwtTokenParameters> configure = null)
        {
            var jwtTokenParameters = new JwtTokenParameters();
            if (configure != null) configure.Invoke(jwtTokenParameters);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

            configuration.GetSection(JwtTokenParameters.ConfigurationSection).Bind(jwtTokenParameters);

            serviceCollection.AddSingleton(jwtTokenParameters);

            return serviceCollection;
        }

        public static IServiceCollection AddApiClient<TInterface, TImplementation, TImplementationMock, TOptions>(this IServiceCollection serviceCollection, Action<TOptions> configure = null)
            where TOptions : ApiClientOptionsBase, new()
            where TImplementationMock : class, TInterface
            where TImplementation : ApiClientBase<TOptions>, TInterface
            where TInterface : class
        {
            var clientOptions = serviceCollection.AddApiClientInternal(configure);

            if (clientOptions.Mock)
                serviceCollection.AddTransient<TInterface, TImplementationMock>();

            else
                ConfigureHandler(serviceCollection.AddHttpClient<TInterface, TImplementation>(httpClient =>
                {
                    httpClient.BaseAddress = new Uri(clientOptions.BaseUrl);
                }));

            return serviceCollection;
        }

        public static IServiceCollection AddApiClient<TInterface, TImplementation, TOptions>(this IServiceCollection serviceCollection, Action<TOptions> configure = null)
            where TOptions : ApiClientOptionsBase, new()
            where TImplementation : ApiClientBase<TOptions>, TInterface
            where TInterface : class
        {

            var clientOptions = serviceCollection.AddApiClientInternal(configure);

            ConfigureHandler(serviceCollection.AddHttpClient<TInterface, TImplementation>(httpClient =>
            {
                httpClient.BaseAddress = new Uri(clientOptions.BaseUrl);
            }));

            return serviceCollection;
        }

        public static IServiceCollection AddApiClient<TImplementation, TOptions>(this IServiceCollection serviceCollection, Action<TOptions> configure = null)
            where TOptions : ApiClientOptionsBase, new()
            where TImplementation : ApiClientBase<TOptions>

        {
            var clientOptions = serviceCollection.AddApiClientInternal(configure);
            ConfigureHandler(serviceCollection.AddHttpClient<TImplementation>(httpClient =>
            {
                httpClient.BaseAddress = new Uri(clientOptions.BaseUrl);
            }));

            return serviceCollection;
        }

        private static TOptions AddApiClientInternal<TOptions>(this IServiceCollection serviceCollection, Action<TOptions> configure = null)
            where TOptions : ApiClientOptionsBase, new()
        {
            var clientOptions = new TOptions();

            if (configure != null) configure.Invoke(clientOptions);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

            configuration.GetSection(clientOptions.ConfigurationSection).Bind(clientOptions);

            if (!clientOptions.Token.IsValid)
                configuration.GetSection(JwtTokenParameters.ConfigurationSection).Bind(clientOptions.Token);

            serviceCollection.AddSingleton(clientOptions);

            return clientOptions;
        }
    }
}
