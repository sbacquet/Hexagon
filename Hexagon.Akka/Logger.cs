using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hexagon.AkkaImpl
{
    public class Logger : Hexagon.ILogger
    {
        Akka.Event.ILoggingAdapter _logger;
        public Logger(Akka.Event.ILoggingAdapter akkaLogger)
        {
            _logger = akkaLogger;
        }

        public bool IsDebugEnabled => _logger.IsDebugEnabled;

        public bool IsInfoEnabled => _logger.IsInfoEnabled;

        public bool IsWarningEnabled => _logger.IsWarningEnabled;

        public bool IsErrorEnabled => _logger.IsErrorEnabled;

        public void Debug(string format, params object[] args)
            => _logger.Debug(format, args);

        public void Error(string format, params object[] args)
            => _logger.Error(format, args);

        public void Error(Exception cause, string format, params object[] args)
            => _logger.Error(cause, format, args);

        public void Info(string format, params object[] args)
            => _logger.Info(format, args);

        public bool IsEnabled(LogLevel logLevel) 
            => _logger.IsEnabled((Akka.Event.LogLevel)logLevel);

        public void Log(LogLevel logLevel, string format, params object[] args)
            => _logger.Log((Akka.Event.LogLevel)logLevel, format, args);

        public void Warning(string format, params object[] args)
            => _logger.Warning(format, args);
    }
}
