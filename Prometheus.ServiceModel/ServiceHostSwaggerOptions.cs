using System.Collections.Generic;

namespace Prometheus.ServiceModel
{
    /// <summary>
    ///     Configuration options object for the SwaggerGen middleware.
    /// </summary>
    public class ServiceHostSwaggerOptions
    {
        /// <summary>
        ///     Gets indication, if the service host will be exposing a swagger DOC endpoint.
        /// </summary>
        public bool UseSwagger { get; set; }

        /// <summary>
        ///     Gets the swagger endpoint title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        ///     Gets the versioning for the swagger endpoint.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        ///     Gets the description for the swagger endpoint.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        ///     Gets the Terms-Of-Service policy document or URL.
        /// </summary>
        public string TermsOfService { get; set; }

        /// <summary>
        ///     Gets the collection of all Xml-Doc generated documentation files.
        /// </summary>
        public IEnumerable<string> XmlDocFilenames { get; set; }
    }
}