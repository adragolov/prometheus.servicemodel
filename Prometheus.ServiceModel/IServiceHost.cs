using Microsoft.Extensions.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus.ServiceModel
{
    /// <summary>
    ///     Interface model for a service host application (e.g. a microservice).
    /// </summary>
    public interface IServiceHost
    {
        /// <summary>
        ///     Stores the current service host status information.
        /// </summary>
        ServiceHostStatus HostStatus { get; }
        /// <summary>
        ///     Stores the most recent error code encountered during service host run interval.
        /// </summary>
        int? HostStatusErrorCode { get; }
        /// <summary>
        ///     Stores the root configuration object of the service host.
        /// </summary>
        IConfigurationRoot HostConfiguration { get; }
        /// <summary>
        ///     Gets an indication, if the service host has performed a bootstrap.
        /// </summary>
        bool HasBootstrapped { get; }
        /// <summary>
        ///     Stores the service host bootstrap event meta data, if the service host has 
        ///     ever performed any.
        /// </summary>
        ServiceHostBootstrapEvent BootstrapEvent { get; }
        /// <summary>
        ///     Gets the currently active service host settings.
        /// </summary>
        ServiceHostSettings Settings { get; }
        /// <summary>
        ///     Gets an indication, if the service host is running.
        /// </summary>
        bool IsRunning { get; }
        /// <summary>
        ///     Stores the timestamp for the next iteration of the service host.
        /// </summary>
        DateTime? NextIterationAt { get; }

        /// <summary>
        ///     Preconfigures the service host and then starts it.
        /// </summary>
        /// <param name="args">Running arguments list.</param>
        void ConfigureAndRun(string[] args);
        /// <summary>
        ///     Performs a forced start of the service host iteration task.
        /// </summary>
        /// <param name="cancellation"></param>
        Task ForceStartIterationAsync(CancellationToken cancellation = default(CancellationToken));
        /// <summary>
        ///     Retrieves the list of the service host bindings at runtime.
        /// </summary>
        string[] GetHostBindings();
        /// <summary>
        ///     Starts and runs the service host.
        /// </summary>
        /// <param name="args">Arguments list, usually passed from the command line.</param>
        /// <param name="cancellation">Cancellation token.</param>
        Task RunHostAsync(string[] args, CancellationToken cancellation = default(CancellationToken));
        /// <summary>
        ///     Stops the service host if running.
        /// </summary>
        /// <param name="cancellation">Cancellation token.</param>
        Task StopHostAsync(CancellationToken cancellation = default(CancellationToken));
    }
}