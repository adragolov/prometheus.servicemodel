using System;

namespace Prometheus.ServiceModel
{
    /// <summary>
    ///     Information object about the runtime state of a service host.
    /// </summary>
    public class ServiceHostRuntimeInformation
    {
        /// <summary>
        ///     Optionally stores the underlying error code of the most recent runtime error.
        /// </summary>
        public int? HostStatusErrorCode { get; set; }

        /// <summary>
        ///     Gets an indication, if the service host has performed a bootstrap.
        /// </summary>
        public bool HasBootstrapped { get; set; }

        /// <summary>
        ///     Gets the current service host status.
        /// </summary>
        public ServiceHostStatus HostStatus { get; set; }

        /// <summary>
        ///     If the service host has already performed bootstrap, stores additional 
        ///     data specific for the bootstrap event.
        /// </summary>
        public ServiceHostBootstrapEvent BootstrapEvent { get; set; }

        /// <summary>
        ///     Gets the currently active host settings.
        /// </summary>
        public ServiceHostSettings Settings { get; set; }

        /// <summary>
        ///     Gets an indication if the service host is configured AND running.
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        ///     Optionally stores an indicative interval for the next processing iteration
        ///     of the service host (if available).
        /// </summary>
        public TimeSpan? NextIteration { get; set; }
    }
}
