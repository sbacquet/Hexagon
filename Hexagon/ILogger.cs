using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hexagon
{
    public enum LogLevel
    {
        //
        // Summary:
        //     The debug log level.
        DebugLevel = 0,
        //
        // Summary:
        //     The information log level.
        InfoLevel = 1,
        //
        // Summary:
        //     The warning log level.
        WarningLevel = 2,
        //
        // Summary:
        //     The error log level.
        ErrorLevel = 3
    }

    public interface ILogger
    {
        //
        // Summary:
        //     Check to determine whether the Akka.Event.LogLevel.DebugLevel is enabled.
        bool IsDebugEnabled { get; }
        //
        // Summary:
        //     Check to determine whether the Akka.Event.LogLevel.InfoLevel is enabled.
        bool IsInfoEnabled { get; }
        //
        // Summary:
        //     Check to determine whether the Akka.Event.LogLevel.WarningLevel is enabled.
        bool IsWarningEnabled { get; }
        //
        // Summary:
        //     Check to determine whether the Akka.Event.LogLevel.ErrorLevel is enabled.
        bool IsErrorEnabled { get; }

        //
        // Summary:
        //     Logs a Akka.Event.LogLevel.DebugLevel message.
        //
        // Parameters:
        //   format:
        //     The message that is being logged.
        //
        //   args:
        //     An optional list of items used to format the message.
        void Debug(string format, params object[] args);
        //
        // Summary:
        //     Logs a Akka.Event.LogLevel.ErrorLevel message.
        //
        // Parameters:
        //   format:
        //     The message that is being logged.
        //
        //   args:
        //     An optional list of items used to format the message.
        void Error(string format, params object[] args);
        //
        // Summary:
        //     Logs a Akka.Event.LogLevel.ErrorLevel message and associated exception.
        //
        // Parameters:
        //   cause:
        //     The exception associated with this message.
        //
        //   format:
        //     The message that is being logged.
        //
        //   args:
        //     An optional list of items used to format the message.
        void Error(Exception cause, string format, params object[] args);
        //
        // Summary:
        //     Logs a Akka.Event.LogLevel.InfoLevel message.
        //
        // Parameters:
        //   format:
        //     The message that is being logged.
        //
        //   args:
        //     An optional list of items used to format the message.
        void Info(string format, params object[] args);
        //
        // Summary:
        //     Determines whether a specific log level is enabled.
        //
        // Parameters:
        //   logLevel:
        //     The log level that is being checked.
        //
        // Returns:
        //     true if the specified level is enabled; otherwise false.
        bool IsEnabled(LogLevel logLevel);
        //
        // Summary:
        //     Logs a message with a specified level.
        //
        // Parameters:
        //   logLevel:
        //     The level used to log the message.
        //
        //   format:
        //     The message that is being logged.
        //
        //   args:
        //     An optional list of items used to format the message.
        void Log(LogLevel logLevel, string format, params object[] args);
        //
        // Summary:
        //     Logs a Akka.Event.LogLevel.WarningLevel message.
        //
        // Parameters:
        //   format:
        //     The message that is being logged.
        //
        //   args:
        //     An optional list of items used to format the message.
        void Warning(string format, params object[] args);
    }
}
