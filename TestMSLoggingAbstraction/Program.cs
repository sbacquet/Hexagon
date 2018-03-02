using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;
using Serilog;

namespace TestMSLoggingAbstraction
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new Serilog.LoggerConfiguration()
                .MinimumLevel.Is(Serilog.Events.LogEventLevel.Information)
                .MinimumLevel.Override("TestMSLoggingAbstraction.TestLog", Serilog.Events.LogEventLevel.Verbose)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level,-7:w7}] {SourceContext}{Scope} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
            var serviceCollection = new ServiceCollection()
                .AddLogging(builder => builder.AddSerilog())
                .AddTransient<TestLog>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            serviceProvider.GetService<TestLog>().DoSomeStuff();
            Log.CloseAndFlush();
        }
    }

    public class TestLog
    {
        Microsoft.Extensions.Logging.ILogger _logger;
        public TestLog(ILogger<TestLog> logger)
        {
            _logger = logger;
        }
        public class Toto
        {
            public int A;
        }

        public void DoSomeStuff()
        {
            var position1 = new { A = 1 };
            var position2 = new { B = 2 };
            using (_logger.BeginScope("my scope"))
            {
                _logger.LogTrace("Trace");
                _logger.LogDebug("Debug");
                _logger.LogInformation("Processed {Position1} {Position2}", position1, position2);
                _logger.LogWarning("Warning");
                _logger.LogError("Error");
                _logger.LogCritical("Critical error");
            }
        }
    }
}
