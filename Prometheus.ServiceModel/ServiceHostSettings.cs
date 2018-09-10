namespace Prometheus.ServiceModel
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     Configuration settings object for a single service host instance.
    /// </summary>
    public class ServiceHostSettings
    {
        /// <summary>
        ///     Configures the iteration interval for host planned iterations.
        ///     The host will not perform iterations, if the interval is undefined or empty
        ///     (<seealso cref="TimeSpan.Zero"/>).
        /// </summary>
        public TimeSpan? IterationInterval { get; set; }

        /// <summary>
        ///     Stores the identifier of the business domain the service host resides in, for example 'Security'.
        ///     Optional.
        /// </summary>
        public string ServiceCategory { get; set; }

        /// <summary>
        ///     Stores an optional meta data about the service host user-friendly service name, 
        ///     for example 'Identity Service'.
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        ///     Stores an optional meta data about the service host user-friendly description text, 
        ///     for example 'Service responsible for authenticating and authorizing client requests.'.
        /// </summary>
        public string ServiceDescription { get; set; }

        /// <summary>
        ///     Stores the unique identifier value for the service host. Optionally used in scenarios where multiple
        ///     instances of the service host are deployed.
        /// </summary>
        public Guid? Uid { get; set; }
        
        /// <summary>
        ///     Key-value store for additional meta-data associated with the service host.
        /// </summary>
        public Dictionary<string, object> MetaDataInfo { get; set; }
    }
}