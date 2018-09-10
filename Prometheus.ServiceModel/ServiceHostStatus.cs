namespace Prometheus.ServiceModel
{
    /// <summary>
    ///     Enumerates the possible states of a running service host.
    /// </summary>
    public enum ServiceHostStatus
    {
        /// <summary>
        ///     The host is initialising and yet not available.
        /// </summary>
        Initialising = 100,

        /// <summary>
        ///     The host is performing a bootstrap operation.
        /// </summary>
        Bootstrapping = 200,

        /// <summary>
        ///     The host is obtaining configuration.
        /// </summary>
        Configuring = 300,

        /// <summary>
        ///     The host is configured and starting up.
        /// </summary>
        StartingUp = 400,

        /// <summary>
        ///     The host is started and currently performing a logical iteration.
        /// </summary>
        ProcessingIteration = 500,

        /// <summary>
        ///     The host is started and not performing any logical iterations.
        /// </summary>
        Idle = 600,

        /// <summary>
        ///     The host has failed execution and currently down.
        /// </summary>
        Failed = -100,

        /// <summary>
        ///     The host last iteration has failed, but the host is still running.
        /// </summary>
        IterationFailed = -200
    }
}