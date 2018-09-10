using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Prometheus.ServiceModel
{
    /// <summary>
    ///     Interface model for the service host startup process.
    /// </summary>
    public interface IServiceHostStartup
    {
        /// <summary>
        ///     Gets the configuration root for the service host.
        ///     Available for AspNetCore / web hosts.
        /// </summary>
        IConfiguration Configuration { get; }

        /// <summary>
        ///     Gets the runtime information object for the service host.
        ///     Available for AspNetCore / web hosts.
        /// </summary>
        IHostingEnvironment Environment { get; }

        /// <summary>
        ///     Gets the default settings used by the service host if no custom settings are used.
        /// </summary>
        ServiceHostSettings DefaultHostSettings { get; }

        /// <summary>
        ///     Gets the active settings used by the service host.
        /// </summary>
        ServiceHostSettings HostSettings { get; }

        /// <summary>
        ///     Gets the unique ID of the service host instance. Initialized at service host startup.
        /// </summary>
        System.Guid GetHostInstanceId();

        /// <summary>
        ///     Gets the URI associated with the service host.
        /// </summary>
        string GetHostUri();

        /// <summary>
        ///     Gets the active service host bindings (IP address and port number).
        /// </summary>
        string[] GetHostBindings();

        /// <summary>
        ///     Gets the collection IPv4 network interface addresses for the service host.
        /// </summary>
        string[] GetIPv4NetworkAddresses();

        /// <summary>
        ///     Applies new service host settings.
        /// </summary>
        /// <param name="settings">The new settings object. If null, the default settings will be used.</param>
        void ChangeHostSettings(ServiceHostSettings settings);

        /// <summary>
        ///     Creates the model for the service host which may be used a snapshot for future
        ///     resume or resurrect of a dead service.
        /// </summary>
        ServiceHostModel BuildServiceHostModel();
    }
}