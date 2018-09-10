namespace Prometheus.ServiceModel
{
    using System;

    /// <summary>
    ///     Stores information about the bootstrap of a service host.
    /// </summary>
    public class ServiceHostBootstrapEvent
    {
        /// <summary>
        ///     Stores an uniquelly generated identifier of the bootstrap event.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        ///     Gets the timestamp of the bootstrap event.
        /// </summary>
        public DateTimeOffset TimestampUTC { get; set; }

        /// <summary>
        ///     Gets the timestamp as a UNIX timestamp of the bootstrap event.
        /// </summary>
        public long UnixTimestampUTC { get; set; }
        
        /// <summary>
        ///     Creates a new bootstrap event for the current time.
        /// </summary>
        public static ServiceHostBootstrapEvent CreateNew()
        {
            var utcNow = DateTimeOffset.UtcNow;

            return new ServiceHostBootstrapEvent
            {
                Id = Guid.NewGuid(),
                TimestampUTC = utcNow,
                UnixTimestampUTC = utcNow.ToUnixTimeMilliseconds()
            };
        }    
    }
}