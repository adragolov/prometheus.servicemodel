using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

namespace Prometheus.ServiceModel
{
    /// <summary>
    ///     Baseline service host implementation used within the Prometheus framework and domain.
    ///     Preferably used in more monolitic architectures in order to ensure cohesive integration of
    ///     all services running in the business domain.
    /// </summary>
    /// <typeparam name="THostStartup">
    ///     The concrete startup class implementation for the service host. 
    ///     Any additional startup configuration and dependency injection logic needs to be specified in a 
    ///     derived class of the <seealso cref="ServiceHostStartup"/> class.
    /// </typeparam>
    public class ServiceHost<THostStartup> : IServiceHost where THostStartup : ServiceHostStartup
    {
        /// <summary>
        ///     Container for the domain specific error codes used by the service host implementation.
        /// </summary>
        public static class ErrorCodes
        {
            /// <summary>
            ///     General fault error code -
            ///     The error is either unknown or does not have a direct impact for the service operation.
            /// </summary>
            public const int GeneralFault = 20001;
            /// <summary>
            ///     Bootstrap failed code -
            ///     The service host has failed during bootstrap operation.
            /// </summary>
            public const int BootstrapFailed = 30001;
            /// <summary>
            ///     Configuration failed code - 
            ///     The service host has failed during the configuration phase.
            /// </summary>
            public const int ConfigurationFailed = 30101;
            /// <summary>
            ///     Startup failed code - 
            ///     The service host has failed during startup. 
            ///     This usually indicates a fatal / unrecoverable error.
            /// </summary>
            public const int StartupFailed = 30201;
            /// <summary>
            ///     Iteration failed code - 
            ///     The most recent service host iteration has failed.
            /// </summary>
            public const int IterationFailed = 30301;
        }

        /// <summary>
        ///     Container for the default settings used by the service host.
        /// </summary>
        public static class Defaults
        {
            /// <summary>
            ///     The default interval after startup used to trigger the bootstrap of the service host.
            ///     <seealso cref="ServiceHost{THostStartup}.HostStartupTaskWorker"/>.
            /// </summary>
            public static TimeSpan BootstrapDelay { get; }
                = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        ///     Initializes a new instance of the service host class.
        /// </summary>
        /// <param name="startupWorker">
        ///     The activity task that will be performed as part of the bootstrap of the service host. Optional.
        /// </param>
        /// <param name="iterationWorker">
        ///     The activity task taht will be performed on an iteration basis by the service host (e.g. repetative scheduled task). Optional.
        /// </param>
        public ServiceHost(
            Func<Task> startupWorker = null,
            Func<Task> iterationWorker = null)
        {

            HostStartupTaskWorker = startupWorker;
            HostIterationWorker = iterationWorker;
        }

        /// <summary>
        ///     General token used to propagate cancellation of the service host activities.
        /// </summary>
        public readonly CancellationToken GlobalCancellation = new CancellationToken();
        /// <summary>
        ///     Thread lock used during configuration of the service host.
        /// </summary>
        protected readonly object ConfigureAndRunLock = new object();
        /// <summary>
        ///     Thread lock used during the web host management operations.
        /// </summary>
        protected readonly object WebServerHostLock = new object();
        /// <summary>
        ///     Iteration lock used during iterations of the service host.
        /// </summary>
        protected readonly object IterationLock = new object();

        /// <summary>
        ///     Stores the activity task that will be performed as part of the bootstrap of the service host. Optional.
        /// </summary>
        public Func<Task> HostStartupTaskWorker { get; set; }

        /// <summary>
        ///     Stores the activity task taht will be performed on an iteration basis by the service host (e.g. repetative 
        ///     scheduled task). Optional.
        /// </summary>
        public Func<Task> HostIterationWorker { get; set; }

        /// <summary>
        ///     Stores the top-level logger (Serilog-like) used by the service host.
        /// </summary>
        public ILogger SelfLog { get; protected set; }

        /// <summary>
        ///     Timer object used for rising iteration events during the service host lifetime.
        /// </summary>
        protected Timer IterationTimer { get; set; }

        /// <summary>
        ///     Stores the concrete iteration logic task object used during service host iterations.
        /// </summary>
        protected Task IterationTask { get; set; }

        /// <summary>
        ///     Timer object used for rising the bootstrap event of the service host.
        /// </summary>
        protected Timer BootstrapTimer { get; set; }

        /// <summary>
        ///     Persists the service web host running task.
        /// </summary>
        protected Task _WebServerHostRunTask;

        /// <summary>
        ///     Persists the service web host stopping task.
        /// </summary>
        protected Task _WebServerHostStopTask;

        /// <summary>
        ///     Stores a reference to the concrete web service host runtime instance. Available
        ///     for AspNetCore scenarios only.
        /// </summary>
        protected IWebHost WebServerHost { get; set; }

        /// <summary>
        ///     Stores the current service host status information.
        /// </summary>
        public ServiceHostStatus HostStatus { get; protected set; } = ServiceHostStatus.Initialising;

        /// <summary>
        ///     Stores the most recent error code encountered during service host run interval.
        /// </summary>
        public int? HostStatusErrorCode { get; protected set; }

        /// <summary>
        ///     Stores the root configuration object of the service host.
        /// </summary>
        public IConfigurationRoot HostConfiguration { get; protected set; }

        /// <summary>
        ///     Stores the timestamp for the next iteration of the service host.
        /// </summary>
        public DateTime? NextIterationAt { get; protected set; }

        /// <summary>
        ///     Gets an indication, if the service host has performed a bootstrap.
        /// </summary>
        public virtual bool HasBootstrapped
        {
            get
            {
                return BootstrapEvent != null;
            }
        }

        /// <summary>
        ///     Stores the service host bootstrap event meta data, if the service host has 
        ///     ever performed any.
        /// </summary>
        public ServiceHostBootstrapEvent BootstrapEvent { get; protected set; }

        /// <summary>
        ///     Gets the currently active service host settings.
        /// </summary>
        public ServiceHostSettings Settings { get; protected set; }

        /// <summary>
        ///     Gets an indication, if the service host is running.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        ///     Preconfigures the service host and then starts it.
        /// </summary>
        /// <param name="args">Running arguments list.</param>
        public virtual void ConfigureAndRun(string[] args)
        {
            try
            {
                HostConfiguration = BuildHostConfiguration(args);

                RunHostAsync(args).Wait();
            }
            catch (Exception e)
            {
                HostStatus = ServiceHostStatus.Failed;

                if (!HostStatusErrorCode.HasValue)
                {
                    HostStatusErrorCode = ErrorCodes.GeneralFault;
                }

                SelfLog?.Fatal(e,
                              "Fatal error while running the host. Error code is {ErrorCode}.",
                              HostStatusErrorCode);
                throw;
            }
        }

        /// <summary>
        ///     Stops the service host if running.
        /// </summary>
        /// <param name="cancellation">Cancellation token.</param>
        public virtual Task StopHostAsync(CancellationToken cancellation = default(CancellationToken))
        {
            lock (WebServerHostLock)
            {
                if (IsRunning)
                {
                    IsRunning = false;

                    _WebServerHostStopTask = WebServerHost.StopAsync(cancellation);

                    return _WebServerHostStopTask;
                }
                else
                {
                    return Task.FromResult(0);
                }
            }
        }

        /// <summary>
        ///     Starts and runs the service host.
        /// </summary>
        /// <param name="args">Arguments list, usually passed from the command line.</param>
        /// <param name="cancellation">Cancellation token.</param>
        public virtual Task RunHostAsync(string[] args, CancellationToken cancellation = default(CancellationToken))
        {
            lock (WebServerHostLock)
            {
                if (!IsRunning)
                {
                    IsRunning = true;

                    WebServerHost = BuildWebServerHost(args);

                    ScheduleBootstrap();

                    _WebServerHostRunTask = WebServerHost.RunAsync(cancellation);

                    return _WebServerHostRunTask;
                }
                else
                {
                    return _WebServerHostRunTask;
                }
            }
        }

        /// <summary>
        ///     Creates a new service isolated scope, used for resolving scoped dependencies at runtime.
        /// </summary>
        public virtual IServiceScope CreateServiceScope()
        {
            if (!IsRunning || WebServerHost == null)
            {
                throw new InvalidOperationException("Web host is not running.");
            }
            
            return WebServerHost.Services.CreateScope();
        }

        /// <summary>
        ///     Attempts the dependency injection resolution for a given service type.
        ///     Use with transient and singleton service types.
        /// </summary>
        /// <typeparam name="T">The type of the dependency service.</typeparam>
        public virtual T ResolveDependency<T>()
        {
            if (!IsRunning || WebServerHost == null)
            {
                throw new InvalidOperationException("Web host is not running.");
            }

            return WebServerHost.Services.GetService<T>();
        }

        /// <summary>
        ///     Attempts the dependency injection resolution for a given service type.
        ///     Use with scoped service types.
        /// </summary>
        /// <param name="scope">The dependency scope, required.</param>
        /// <typeparam name="T">The type of the dependency service.</typeparam>
        public virtual T ResolveDependency<T>(IServiceScope scope)
        {
            if (!IsRunning || WebServerHost == null)
            {
                throw new InvalidOperationException("Web host is not running.");
            }

            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            return scope.ServiceProvider.GetService<T>();
        }

        /// <summary>
        ///     Retrieves the list of the service host bindings at runtime.
        /// </summary>
        public virtual string[] GetHostBindings()
        {
            if (IsRunning && WebServerHost != null)
            {
                var features = WebServerHost.ServerFeatures;

                var serverAddressFeature = (IServerAddressesFeature)features[typeof(IServerAddressesFeature)];

                return serverAddressFeature.Addresses.ToArray();
            }

            throw new InvalidOperationException("Web host is not running.");
        }

        /// <summary>
        ///     Prepares the service host configuration object.
        ///     Uses environment variables (lower priority) and command line arguments (higher priority)
        ///     for configuration. The default configuration environment is Production.
        /// </summary>
        /// <param name="commandLineArgs">
        ///     The command line arguments of the service host.
        /// </param>
        protected virtual IConfigurationRoot BuildHostConfiguration(string[] commandLineArgs)
        {
            var appSettingsFilepath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

            if (!File.Exists(appSettingsFilepath))
            {
                throw new ApplicationException(
                    $"File '{appSettingsFilepath}' is required but could not be found");
            }

            var configurationBuilder = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddCommandLine(commandLineArgs)
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            string environmentFromCommandLine = null;

            if (commandLineArgs != null && commandLineArgs.Length > 0)
            {
                var commandLineProvider = new CommandLineConfigurationProvider(commandLineArgs);

                commandLineProvider.Load();

                string env = null;

                if (commandLineProvider.TryGet("environment", out env))
                {
                    environmentFromCommandLine = env;
                }
            }

            var environmentName = environmentFromCommandLine ??
                (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production");

            configurationBuilder = configurationBuilder.AddJsonFile($"appsettings.{environmentName}.json", optional: true);

            return configurationBuilder.Build();
        }

        /// <summary>
        ///     Prepares the concrete web service host instance for running.
        /// </summary>
        /// <param name="commandLineArgs">
        ///     The command line arguments of the service host.
        /// </param>
        protected virtual IWebHost BuildWebServerHost(string[] commandLineArgs)
        {
            if (HostConfiguration == null)
            {
                HostConfiguration = BuildHostConfiguration(commandLineArgs);
            }

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(HostConfiguration)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .CreateLogger();

            SelfLog = Log.Logger;

            return WebHost.CreateDefaultBuilder(commandLineArgs)
                          .UseConfiguration(HostConfiguration)
                          .UseStartup<THostStartup>()
                          .UseSerilog(SelfLog, false)
                          .ConfigureServices(services => {

                              services.AddSingleton<IServiceHost>((arg) => {

                                  return this;
                              });

                              services.AddSingleton((arg) => {

                                  return SelfLog;
                              });
                          })
                          .Build();
        }

        /// <summary>
        ///     Reschedules the bootstrap event timer. <seealso cref="BootstrapTimerCallback(object)"/>.
        /// </summary>
        protected virtual void ScheduleBootstrap()
        {
            if (BootstrapTimer != null)
            {
                BootstrapTimer.Dispose();
            }

            BootstrapTimer = new Timer(BootstrapTimerCallback, null, (int)Defaults.BootstrapDelay.TotalMilliseconds, Timeout.Infinite);
        }

        /// <summary>
        ///     Event handler of the bootstrap event timer.
        /// </summary>
        /// <param name="o">The context of the timer event.</param>
        protected void BootstrapTimerCallback(object o)
        {
            BootstrapTimerCallbackAsync(GlobalCancellation).Wait();
        }

        /// <summary>
        ///     The task performed as part of the bootstrap event callback.
        /// </summary>
        /// <param name="cancellation">Task cancellation token.</param>
        protected virtual async Task BootstrapTimerCallbackAsync(CancellationToken cancellation = default(CancellationToken))
        {
            try
            {
                SelfLog?.Information("Starting host boostrap.");

                HostStatus = ServiceHostStatus.Bootstrapping;

                HostStatusErrorCode = default(int?);

                var bindings = GetHostBindings();

                if (bindings == null || bindings.Length == 0)
                {
                    throw new ApplicationException("Web host bindings are not configured.");
                }
                else
                {
                    foreach (var b in bindings)
                    {
                        SelfLog?.Information("Host binding available at {HostBinding}.", b);
                    }

                    await LoadHostSettingsAsync(cancellation);

                    if (HostStartupTaskWorker != null)
                    {
                        HostStatus = ServiceHostStatus.StartingUp;

                        HostStatusErrorCode = default(int?);

                        try
                        {
                            var startupTask = HostStartupTaskWorker();

                            if (startupTask != null)
                            {
                                await startupTask;
                            }
                        }
                        catch (Exception workerException)
                        {
                            SelfLog?.Error(workerException, "Exception while processing startup worker.");

                            HostStatus = ServiceHostStatus.Failed;

                            HostStatusErrorCode = ErrorCodes.StartupFailed;

                            throw;
                        }
                    }

                    BootstrapEvent = ServiceHostBootstrapEvent.CreateNew();
                    
                    SelfLog?.Information("Bootstrap complete.");

                    HostStatus = ServiceHostStatus.Idle;

                    HostStatusErrorCode = default(int?);

                    if (Settings.IterationInterval.HasValue && Settings.IterationInterval.Value.TotalMilliseconds > 0)
                    {
                        await PerformIterationAsync(cancellation);
                    }
                }
            }
            catch (Exception e)
            {
                HostStatus = ServiceHostStatus.Failed;

                if (!HostStatusErrorCode.HasValue)
                {
                    HostStatusErrorCode = ErrorCodes.BootstrapFailed;
                }

                SelfLog?.Fatal(e, "Fatal exception while performing bootstrap. Error code is {ErrorCode}.",
                          HostStatusErrorCode);

                throw;
            }
        }

        /// <summary>
        ///     Loads the service host settings asynchroniously.
        /// </summary>
        /// <param name="cancellation">Task cancellation token.</param>
        protected virtual Task LoadHostSettingsAsync(CancellationToken cancellation = default(CancellationToken))
        {
            try
            {
                HostStatus = ServiceHostStatus.Configuring;

                HostStatusErrorCode = default(int?);

                var bindings = GetHostBindings();

                if (bindings == null || bindings.Length == 0)
                {
                    throw new ApplicationException("Web host bindings are not configured.");
                }
                else
                {
                    var binding = bindings[0];

                    var startup = ResolveDependency<IServiceHostStartup>();

                    Settings = startup.HostSettings ?? startup.DefaultHostSettings;

                    HostStatus = ServiceHostStatus.Idle;

                    SelfLog?.Information("Application configuration loaded.");

                    return Task.FromResult(0);
                }
            }
            catch (Exception e)
            {
                HostStatus = ServiceHostStatus.Failed;

                HostStatusErrorCode = ErrorCodes.ConfigurationFailed;

                SelfLog?.Error(e, "Exception while loading host settings. Error code is {ErrorCode}", HostStatusErrorCode);

                throw;
            }
        }

        /// <summary>
        ///     Resets the iteration event timer.
        /// </summary>
        protected virtual void ResetIterationTimer()
        {
            if (IterationTimer != null)
            {
                IterationTimer.Dispose();

                IterationTimer = null;
            }
        }

        /// <summary>
        ///     Event handler of the iteration event timer.
        /// </summary>
        /// <param name="o">The context of the timer event.</param>
        protected virtual void PerformIterationTimerCallback(object state)
        {
            ResetIterationTimer();

            PerformIterationAsync(GlobalCancellation).Wait();
        }

        /// <summary>
        ///     The iteration task performed during service host iterations.
        /// </summary>
        /// <param name="o">The context of the timer event.</param>
        protected virtual async Task PerformIterationAsync(CancellationToken cancellation = default(CancellationToken))
        {
            if (HostIterationWorker != null)
            {
                HostStatus = ServiceHostStatus.ProcessingIteration;

                HostStatusErrorCode = default(int?);

                try
                {

                    lock (IterationLock)
                    {
                        IterationTask = HostIterationWorker();
                    }

                    if (IterationTask != null)
                    {
                        await IterationTask;
                    }

                    HostStatus = ServiceHostStatus.Idle;

                    HostStatusErrorCode = default(int?);

                    await LoadHostSettingsAsync(cancellation);

                }
                catch (Exception e)
                {
                    HostStatus = ServiceHostStatus.IterationFailed;

                    if (!HostStatusErrorCode.HasValue)
                    {

                        HostStatusErrorCode = ErrorCodes.IterationFailed;
                    }

                    SelfLog?.Error(e,
                                  "Exception while processing iteration task. Error code is {ErrorCode}",
                                  HostStatusErrorCode);
                }
                finally
                {

                    lock (IterationLock)
                    {
                        IterationTask = null;
                    }
                }
            }

            if (Settings.IterationInterval.HasValue && Settings.IterationInterval.Value.TotalMilliseconds > 0)
            {
                ResetIterationTimer();

                SelfLog?.Information("Configuring host iteration in {IterationInterval}", Settings.IterationInterval);

                NextIterationAt = DateTime.UtcNow.Add(Settings.IterationInterval.Value);

                IterationTimer = new Timer(PerformIterationTimerCallback, null, (int)Settings.IterationInterval.Value.TotalMilliseconds, Timeout.Infinite);
            }
            else
            {

                NextIterationAt = null;
            }
        }

        /// <summary>
        ///     Performs a forced start of the service host iteration task.
        /// </summary>
        /// <param name="cancellation"></param>
        public virtual Task ForceStartIterationAsync(CancellationToken cancellation = default(CancellationToken))
        {
            Task result = null;

            lock (IterationLock)
            {
                if (IterationTask != null)
                {
                    result = IterationTask;
                }
            }

            if (result != null)
            {
                return result;
            }
            else
            {
                return PerformIterationAsync(cancellation);
            }
        }
    }
}