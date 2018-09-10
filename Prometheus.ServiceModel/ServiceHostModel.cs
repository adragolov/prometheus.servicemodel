namespace Prometheus.ServiceModel
{
    using System;

    /// <summary>
    ///     Object that stores the pre-configuration settings of a service host (microservice).
    /// </summary>
    public class ServiceHostModel
    {
        /// <summary>
        ///     Gets the unique identifier of the host, generated at warm up times.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        ///     Gets the unique resource identifier (URI) associated with the host.
        /// </summary>
        public string Uri { get; set; }

        /// <summary>
        ///     Gets the name of the configuration environment, e.g. Development, Staging or Production.
        /// </summary>
        public string Environment { get; set; }

        /// <summary>
        ///     Gets the user friendly name of the microservice.
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        ///     Gets the physical host/vhost name where the microservice is to currently deployed onto.
        /// </summary>
        public string MachineName { get; set; }

        /// <summary>
        ///     Gets the configuration settings used at startup.
        /// </summary>
        public ServiceHostSettings DefaultSettings { get; set; }

        /// <summary>
        ///     Gets the the currently active configuration settings for the microservice.
        /// </summary>
        public ServiceHostSettings Settings { get; set; }

        /// <summary>
        ///     Retrieves a list of the physical bindings of the microservice.
        /// </summary>
        public string[] Bindings { get; set; }

        /// <summary>
        ///     Gets the most recent runtime information of the microservice.
        /// </summary>
        public ServiceHostRuntimeInformation RuntimeInformation { get; set; }
    }
}