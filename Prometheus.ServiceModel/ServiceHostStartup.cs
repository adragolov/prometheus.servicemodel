namespace Prometheus.ServiceModel
{
    using Consul;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Cors.Infrastructure;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Hosting.Server.Features;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using Prometheus.Core.Security;
    using Prometheus.Core.Utility;
    using Prometheus.ServiceModel.Extensions;
    using Serilog;
    using Swashbuckle.AspNetCore.Swagger;
    using System;
    using System.IO;
    using System.Linq;

    /// <summary>
    ///     Generic implementation for the service host startup process used along
    ///     in the Prometheus framework and domains.
    /// </summary>
    public class ServiceHostStartup : IServiceHostStartup
    {
        /// <summary>
        ///     Thread lock for updating service host settings.
        /// </summary>
        protected readonly object SettingsLock = new object();

        /// <summary>
        ///     Gets the unique identifier of the service host, generated at warmup times of the service.
        /// </summary>
        public Guid InstanceId { get; protected set; }

        /// <summary>
        ///     Gets the service host application builder provided by the AspNetCore pipeline.
        /// </summary>
        public IApplicationBuilder ApplicationBuilder { get; protected set; }

        /// <summary>
        ///     Gets the configuration root for the service host.
        ///     Available for AspNetCore / web hosts.
        /// </summary>
        public IConfiguration Configuration { get; protected set; }
        
        /// <summary>
        ///     Gets the runtime information object for the service host.
        ///     Available for AspNetCore / web hosts.
        /// </summary>
        public IHostingEnvironment Environment { get; protected set; }

        /// <summary>
        ///     Gets the application lifetime context for the service host.
        ///     Available for AspNetCore / web hosts.
        /// </summary>
        public IApplicationLifetime Lifetime { get; protected set; }

        /// <summary>
        ///     Gets the collection of configured / dependency injection mapped services.
        /// </summary>
        public IServiceCollection Services { get; protected set; }

        /// <summary>
        ///     Gets the default settings used by the service host if no custom settings are used.
        /// </summary>
        public ServiceHostSettings DefaultHostSettings { get; protected set; }

        /// <summary>
        ///     Gets the active settings used by the service host.
        /// </summary>
        public ServiceHostSettings HostSettings { get; protected set; }

        /// <summary>
        ///     Gets the configured SwaggerDoc options.
        /// </summary>
        public ServiceHostSwaggerOptions SwaggerOptions { get; protected set; }

        /// <summary>
        ///     Gets the configured HashiCorp Consul monitoring options.
        /// </summary>
        public ServiceHostConsulOptions ConsulOptions { get; protected set; }

        /// <summary>
        ///     Initializes the startup class instance.
        /// </summary>
        /// <param name="configuration">The webhost configuration root, injected by the runtime.</param>
        public ServiceHostStartup(IConfiguration configuration)
        {
            Configuration = configuration;
            
            var settingsSection = Configuration.GetSection(nameof(DefaultHostSettings));

            DefaultHostSettings = new ServiceHostSettings();

            settingsSection.Bind(DefaultHostSettings);

            InstanceId = DefaultHostSettings.Uid ?? Guid.NewGuid();
        }

        /// <summary>
        ///     Gets the unique ID of the service host instance. Initialized at service host startup.
        /// </summary>
        public Guid GetHostInstanceId()
        {
            return InstanceId;
        }

        /// <summary>
        ///     Creates the model for the service host which may be used a snapshot for future
        ///     resume or resurrect of a dead service.
        /// </summary>
        public virtual ServiceHostModel BuildServiceHostModel()
        {
            return new ServiceHostModel
            {
                Id = InstanceId,
                Uri = GetHostUri(),
                DefaultSettings = DefaultHostSettings,
                Settings = HostSettings ?? DefaultHostSettings,
                Bindings = GetHostBindings(),
                Environment = Environment.EnvironmentName,
                MachineName = System.Environment.MachineName,
                ApplicationName = Environment.ApplicationName
            };
        }

        /// <summary>
        ///     Applies new service host settings.
        /// </summary>
        /// <param name="settings">The new settings object. If null, the default settings will be used.</param>
        public virtual void ChangeHostSettings(ServiceHostSettings settings)
        {
            lock (SettingsLock)
            {
                HostSettings = settings ?? DefaultHostSettings;
            }
        }

        /// <summary>
        ///     Gets the URI associated with the service host.
        /// </summary>
        public virtual string GetHostUri()
        {
            return $"{Environment.ApplicationName}-{Environment.EnvironmentName}-{InstanceId}";
        }

        /// <summary>
        ///     Gets the active service host bindings (IP address and port number).
        /// </summary>
        public virtual string[] GetHostBindings()
        {
            var features = ApplicationBuilder.ServerFeatures;

            var serverAddressFeatureValue = features[typeof(IServerAddressesFeature)];

            if (serverAddressFeatureValue != null)
            {
                var serverAddressFeature = (IServerAddressesFeature)serverAddressFeatureValue;

                return serverAddressFeature.Addresses.ToArray();
            }
            else
            {
                return new string[0];
            }
        }

        /// <summary>
        ///     Gets the collection IPv4 network interface addresses for the service host.
        /// </summary>
        public string[] GetIPv4NetworkAddresses()
        {
            return NetworkUtility.GetHostIPv4Interfaces()
                .Select(ip => ip.ToString())
                .ToArray();
        }

        /// <summary>
        ///     Configures the authorization policies, their required schemes and claims for the service host.
        /// </summary>
        /// <param name="options">
        ///     The authorization options object, provided by the AspNetCore pipeline.
        /// </param>
        protected virtual void ConfigureAuthorizationOptions(AuthorizationOptions options)
        {
            //
            //  Maintenance team policy - allows generic read on APIs
            //

            options.AddPolicy("RequireMaintenanceTeam", policy => policy
                .RequireClaim("generic-read")
            );

            //
            //  Administrative team policy - allows generic read/write on APIs
            //

            options.AddPolicy("RequireAdministrativeTeam", policy => policy
                .RequireClaim("generic-read")
                .RequireClaim("generic-write")
            );
        }

        /// <summary>
        ///     Configures the authentication schemes and challenges provided by the service host.
        /// </summary>
        /// <param name="options">
        ///     The authentication options object, provided by the AspNetCore pipeline.
        /// </param>
        protected virtual void ConfigureAuthenticationOptions(AuthenticationOptions options)
        {
            options.DefaultScheme = "Bearer";
        }

        /// <summary>
        ///     Configures the cross origin options for the service host.
        /// </summary>
        /// <param name="options">CORS options, provided by the AspNetCore pipeline.</param>
        protected virtual void ConfigureCorsOptions(CorsOptions options)
        {
        }

        /// <summary>
        ///     Configures the top level authentication schemes and authorization policies recognized by the service host.
        /// </summary>
        /// <param name="services">
        ///     The central collection set of services. Provided by the AspNetCore pipeline.
        /// </param>
        protected virtual void ConfigureHostSecurity(IServiceCollection services)
        {
            services.AddAuthentication(ConfigureAuthenticationOptions);

            services.AddAuthorization(ConfigureAuthorizationOptions);

            services.AddCors(ConfigureCorsOptions);
        }

        /// <summary>
        ///     Configures the web host services and dependencies. Injected by the dot net core runtime.
        /// </summary>
        /// <param name="services">
        ///     The central collection set of services. Provided by the AspNetCore pipeline.
        /// </param>
        public virtual void ConfigureServices(IServiceCollection services)
        {
            ConfigureHostSecurity(services);

            services.AddMvc()
                .AddJsonOptions(options =>
                {
                    options.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
                    options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
                });

            services.AddSingleton<IServiceHostStartup>((arg) => {
                
                return this;
            });

            services.AddSingleton((arg) => {

                return Log.Logger;
            });

            services.AddTransient<ISymmetricCipher>((arg) => {
                
                return new Aes256SymmetricCipher();
            });

            services.AddTransient<IHashProvider>((arg) => {

                return new BCryptHashProvider();
            });

            services.Configure<ServiceHostSwaggerOptions>(options => 
            {
                Configuration.GetSection(nameof(ServiceHostSwaggerOptions)).Bind(options);
            });

            services.Configure<ServiceHostConsulOptions>(options =>
            {
                Configuration.GetSection(nameof(ServiceHostConsulOptions)).Bind(options);
            });

            LoadSwaggerOptions(services);

            LoadConsulOptions(services);

            Services = services;
        }

        /// <summary>
        ///     Loads the <seealso cref="SwaggerOptions"/> from the configuration file and
        ///     configures the SwaggerDoc middleware accordingly.
        /// </summary>
        /// <param name="services">
        ///     The central collection set of services. Provided by the AspNetCore pipeline.
        /// </param>
        protected virtual void LoadSwaggerOptions(IServiceCollection services)
        {
            SwaggerOptions = services
                .BuildServiceProvider()
                .GetRequiredService<IOptions<ServiceHostSwaggerOptions>>()
                .Value;

            if (SwaggerOptions.UseSwagger)
            {
                services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc(SwaggerOptions.Title, new Info
                    {
                        Title = SwaggerOptions.Title,
                        Version = SwaggerOptions.Version,
                        Description = SwaggerOptions.Description,
                        TermsOfService = SwaggerOptions.TermsOfService
                    });

                    var basePath = AppContext.BaseDirectory;

                    var xmlFileNames = SwaggerOptions.XmlDocFilenames;

                    if (xmlFileNames != null)
                    {
                        foreach (var filename in xmlFileNames)
                        {
                            var xmlPath = Path.Combine(basePath, filename);

                            if (File.Exists(xmlPath))
                            {
                                c.IncludeXmlComments(xmlPath);
                            }
                        }
                    }
                });
            }
        }

        /// <summary>
        ///     Loads the <seealso cref="ConsulOptions"/> from the configuration file and
        ///     configures the HashiCorp Consul monitoring middleware accordingly.
        /// </summary>
        /// <param name="services">
        ///     The central collection set of services. Provided by the AspNetCore pipeline.
        /// </param>
        protected virtual void LoadConsulOptions(IServiceCollection services)
        {
            ConsulOptions = services
                .BuildServiceProvider()
                .GetRequiredService<IOptions<ServiceHostConsulOptions>>()
                .Value;

            if (ConsulOptions.UseConsulSelfRegistration)
            {
                services.AddScoped<IConsulClient, ConsulClient>(p => new ConsulClient(consulConfig =>
                {
                    consulConfig.Address = new Uri(ConsulOptions.Address);

                    if (!string.IsNullOrEmpty(ConsulOptions.Datacenter))
                    {
                        consulConfig.Datacenter = ConsulOptions.Datacenter;
                    }

                    if (!string.IsNullOrEmpty(ConsulOptions.Token))
                    {
                        consulConfig.Token = ConsulOptions.Token;
                    }
                }));
            }
        }

        /// <summary>
        ///     Configures the web host instance. Injected by the dot net core runtime.
        /// </summary>
        /// <param name="app">The application builder context, injected by the runtime.</param>
        /// <param name="env">The environment context, injected by the runtime.</param>
        /// <param name="appLifetime">The application lifetime context, injected by the runtime.</param>
        public virtual void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime appLifetime)
        {
            ApplicationBuilder = app;

            Environment = env;

            Lifetime = appLifetime;

            ConfigureCorsUsage(app, env);

            app.ApplyUsageForDefaultForwardedHeaders();

            app.ApplyUsageForSwagger(SwaggerOptions);

            app.ApplyUsageForConsul(appLifetime, ConsulOptions);

            app.UseAuthentication();
            
            app.UseMvc();
        }
        
        /// <summary>
        ///     Configures the web host cors policy usage. 
        ///     Called at the beginning of the Configure callback.
        /// </summary>
        /// <param name="app">The application builder context, injected by the runtime.</param>
        /// <param name="env">The environment context, injected by the runtime.</param>
        protected virtual void ConfigureCorsUsage(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseCors();
        }
    }
}