using System;
using Inedo.Diagnostics;

namespace Inedo.Extensions
{
    internal sealed class ShimLogger : ILogger
    {
        private ILogSink log;
        public ShimLogger(ILogSink canonicalLogger)
        {
            this.log = canonicalLogger;
        }

#pragma warning disable CS0067
        public event EventHandler<LogMessageEventArgs> MessageLogged;
#pragma warning restore CS0067

        public void Log(MessageLevel logLevel, string message)
        {
            this.log?.Log(logLevel, message);
        }
    }
}
