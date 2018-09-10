using Consul;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Prometheus.ServiceModel.Extensions
{
    /// <summary>
    ///     Container for useful extensions methods in the Prometheus.ServiceModel domain.
    /// </summary>
    public static class ServiceModelExtensions
    {
        /// <summary>
        ///     Creates a service business object for a registration against a Consul agent.
        /// </summary>
        /// <param name="startup">The runtime host startup instance.</param>
        public static AgentServiceRegistration CreateConsulServiceRegistration(this IServiceHostStartup startup)
        {
            var hostSettings = startup.HostSettings ?? startup.DefaultHostSettings;

            var serviceBindings = startup.GetHostBindings();

            var serviceModel = startup.BuildServiceHostModel();
            var env = serviceModel.Environment;
            var defaultServiceBinding = serviceModel.Bindings.First();
            var defaultServiceBindingUri = new Uri(defaultServiceBinding);
            var ipAddresses = startup.GetIPv4NetworkAddresses();
            var ipString = ipAddresses != null ? string.Join(' ', ipAddresses.Select(ip => ip.ToString())) : string.Empty;
            var bindings = serviceModel.Bindings != null ? string.Join(' ', serviceModel.Bindings) : string.Empty;

            return new AgentServiceRegistration()
            {
                // APPLICATION-ENVIRONMENT-UID
                ID = serviceModel.Uri,
                // APPLICATION-ENVIRONMENT
                Name = $"{serviceModel.ApplicationName}-{env}",
                Address = defaultServiceBindingUri.ToString(),
                Port = defaultServiceBindingUri.Port,
                Tags = new[] {
                        "DotNetCore",
                        "AspNetCore",
                        "Promethean",
                        $"env:{env}"
                    },
                Meta = new Dictionary<string, string>
                    {
                        { "InstanceId", serviceModel.Id.ToString() },
                        { "Uri", serviceModel.Uri},
                        { "IterationInterval", hostSettings.IterationInterval.ToString()},
                        { "Category", hostSettings.ServiceCategory??string.Empty},
                        { "Environment", env},
                        { "IPAddresses", ipString },
                        { "Bindings", bindings }
                    }
            };
        }
    }
}