using Microsoft.Extensions.Logging;

namespace Tuvi.Core.Logging
{
    public static class LoggingExtension
    {
        private static ILoggerFactory _loggerFactory;

        public static ILoggerFactory LoggerFactory
        {
            get
            {
                if (_loggerFactory is null)
                {
                    _loggerFactory = new LoggerFactory();
                }
                return _loggerFactory;
            }
            set { _loggerFactory = value; }
        }
        static class LoggerContainer<T>
        {
            internal static readonly ILogger Logger = LoggerFactory.CreateLogger<T>();
        }

        public static ILogger Log<T>() => LoggerContainer<T>.Logger;
        public static ILogger Log<T>(this T t) => LoggerContainer<T>.Logger;
    }
}
