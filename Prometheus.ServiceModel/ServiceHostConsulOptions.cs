namespace Prometheus.ServiceModel
{
    /// <summary>
    ///     Configuration object for the Consul middleware.
    /// </summary>
    public class ServiceHostConsulOptions
    {
        /// <summary>
        ///     Gets indication, if the service host will be bootstrapping against a Hashicorp Consul 
        ///     monitoring agent.
        /// </summary>
        public bool UseConsulSelfRegistration { get; set; }

        /// <summary>
        ///     Gets the URI address of the Consul monitoring host.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        ///     Gets the datacenter property for the Consul monitoring host.
        /// </summary>
        public string Datacenter { get; set; }

        /// <summary>
        ///     Auth token issued by the Consul monitoring host.
        /// </summary>
        public string Token { get; set; }
    }
}