using Consul;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace Prometheus.ServiceModel.Extensions
{
    /// <summary>
    ///     Extension methods for the AspNetCore pipeline management class, the IApplicationBuilder.
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        ///     Applies a cross origin policy to the web server host that allows any remote origin, header and name.
        /// </summary>
        /// <param name="app">The application builder instance.</param>
        public static IApplicationBuilder ApplyUsageForCorsFromAnyOriginHeaderAndMethod(this IApplicationBuilder app)
        {
            app.UseCors(policyBuilder =>
            {
                policyBuilder
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowAnyOrigin();
            });

            return app;
        }

        /// <summary>
        ///     Applies usage for the default set of forwared headers to the web server host that are required for Kestrel <-> Nginx integration.
        /// </summary>
        /// <param name="app">The application builder instance.</param>
        public static IApplicationBuilder ApplyUsageForDefaultForwardedHeaders(this IApplicationBuilder app)
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            return app;
        }

        /// <summary>
        ///     Applies the SwaggerDoc middleware to the web server host based on the configuration options argument.
        /// </summary>
        /// <param name="app">The application builder instance.</param>
        /// <param name="options">The service host swagger options.</param>
        public static IApplicationBuilder ApplyUsageForSwagger(this IApplicationBuilder app, ServiceHostSwaggerOptions options)
        {
            if (options != null && options.UseSwagger)
            {
                app.UseSwagger();

                if (!string.IsNullOrEmpty(options.Title))
                {
                    app.UseSwaggerUI(c =>
                    {
                        c.SwaggerEndpoint($"/swagger/{options.Title}/swagger.json", options.Title);
                    });
                }
            }

            return app;
        }

        /// <summary>
        ///     Applies HashiCorp Consul middleware to the web server host based on the configuration options argument.
        /// </summary>
        /// <param name="app">The application builder instance.</param>
        /// <param name="appLifetime">The application lifetime context, injected by the runtime.</param>
        /// <param name="options">The service host Consul monitoring options.</param>
        public static IApplicationBuilder ApplyUsageForConsul(
            this IApplicationBuilder app, 
            IApplicationLifetime appLifetime, 
            ServiceHostConsulOptions options)
        {
            if (options != null && options.UseConsulSelfRegistration)
            {
                var logger = app.ApplicationServices.GetRequiredService<Serilog.ILogger>();

                var startup = app.ApplicationServices.GetRequiredService<IServiceHostStartup>();

                var serviceRegistration = startup.CreateConsulServiceRegistration();
                
                appLifetime.ApplicationStarted.Register(() => {
                    try
                    {
                        using (var scope = app.ApplicationServices.CreateScope())
                        using (var consulClient = scope.ServiceProvider.GetRequiredService<IConsulClient>())
                        {
                            consulClient.Agent.ServiceRegister(serviceRegistration);

                            logger.Information("Registering Consul service {ConsulServiceName} with {ConsulServiceId}", serviceRegistration.Name, serviceRegistration.ID);

                            var registerTask = consulClient.Agent.ServiceRegister(serviceRegistration);

                            registerTask.Wait();

                            var services = consulClient.Agent.Services().GetAwaiter().GetResult();

                            var match = services.Response
                                .Select(result => result.Value)
                                .Where(svc => svc.ID.Equals(serviceRegistration.ID))
                                .SingleOrDefault();

                            if (match != null)
                            {
                                logger.Information("Registering Consul API Ping health check for {ConsulServiceName} with {ConsulServiceId}", serviceRegistration.Name, serviceRegistration.ID);

                                var check = new AgentCheckRegistration
                                {
                                    ID = $"PROMETHEAN-API-PING-{serviceRegistration.ID}",
                                    ServiceID = serviceRegistration.ID,
                                    Interval = TimeSpan.FromSeconds(30),
                                    Name = $"Promethean API Ping Status",
                                    Notes = "Status check that verifies the ping endpoint of a Promethean service host.",
                                    TLSSkipVerify = true,
                                    HTTP = $"{match.Address}ping",
                                    Timeout = TimeSpan.FromSeconds(5),
                                };

                                consulClient.Agent.CheckRegister(check).GetAwaiter().GetResult();
                            }
                        }
                    }
                    catch(Exception e)
                    {
                        logger.Error(e, "Error while registering Consul service {ConsulServiceName} with {ConsulServiceId}: {ErrorMessage}", 
                            serviceRegistration.Name, 
                            serviceRegistration.ID,
                            e.Message);
                    }
                });

                appLifetime.ApplicationStopping.Register(() => {
                    try
                    {
                        using (var scope = app.ApplicationServices.CreateScope())
                        using (var consulClient = scope.ServiceProvider.GetRequiredService<IConsulClient>())
                        {
                            logger.Information("De-Registering Consul service {ConsulServiceName} with {ConsulServiceId}", serviceRegistration.Name, serviceRegistration.ID);

                            var unregisterTask = consulClient.Agent.ServiceDeregister(serviceRegistration.ID);

                            unregisterTask.Wait();
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, "Error while de-registering Consul service {ConsulServiceName} with {ConsulServiceId}: {ErrorMessage}",
                            serviceRegistration.Name,
                            serviceRegistration.ID,
                            e.Message);
                    }
                });
            }

            return app;
        }
    }
}