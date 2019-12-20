using System;
using System.Text.RegularExpressions;

namespace GGSQL
{
    public enum LogLevel
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Exception = 3
    }

    public class ServerLogger
    {
        private Regex _regexMsgCleaner = new Regex(@"({|})");
        private string _scriptName;
        private LogLevel _logLevel;

        public ServerLogger(string scriptName, LogLevel logLevel)
        {
            _scriptName = scriptName;
            _logLevel = logLevel;
        }

        public void Info(string method, string msg)
        {
            Info($"[{method}] {msg}");
        }

        public void Info(string msg)
        {
            Log(msg, LogLevel.Info);
        }

        public void Warning(string method, string msg)
        {
            Warning($"[{method}] {msg}");
        }

        public void Warning(string msg)
        {
            Log(msg, LogLevel.Warning);
        }

        public void Error(string method, string msg)
        {
            Error($"[{method}] {msg}");
        }

        public void Error(string msg)
        {
            Log(msg, LogLevel.Error);
        }

        public void Exception(string method, Exception ex)
        {
            var msg = $"[{method}] {ex.Message}";
            Log(msg, LogLevel.Exception);
        }

        private void Log(string msg, LogLevel level)
        {
            if (level >= _logLevel)
            {
                try
                {
                    CitizenFX.Core.Debug.WriteLine($"[GGSQL] {level.ToString()}: [{DateTime.Now.ToString("yyyy-MM-dd-HH:mm:ss:fff")}] {_regexMsgCleaner.Replace(msg, " ")}");
                }
                catch (Exception ex)
                {
                    CitizenFX.Core.Debug.WriteLine($"[GGSQL] [{DateTime.Now.ToString("yyyy-MM-dd-HH:mm:ss:fff")}] [ERROR OCCURED ATTEMPTING TO LOG ERROR MSG]; Error: {ex.Message}");
                }
            }
        }
    }
}
