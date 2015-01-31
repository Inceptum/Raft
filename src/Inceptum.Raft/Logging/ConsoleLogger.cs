using System;
using System.Text;
using System.Threading;

namespace Inceptum.Raft.Logging
{
    public class ConsoleLogger:ILogger
    {
        private readonly string m_Name;

        public ConsoleLogger(string name)
        {
            m_Name = name;
        }


        public void Fatal(string format, params object[] args)
        {
            log("FATAL",format,args);
        }

        public void Error(string format, params object[] args)
        {
            log("ERROR",format, args);
        }

        public void Info(string format, params object[] args)
        {
            log("INFO", format, args);

        }

        public void Debug(string format, params object[] args)
        {
            log("DEBUG", format, args);

        }

        public void Trace(string format, params object[] args)
        {
            log("TRACE", format, args);

        }

        public void Fatal(Exception ex, string format, params object[] args)
        {
            log("FATAL", format, args);
        }

        public void Error(Exception ex, string format, params object[] args)
        {
            log("ERROR", ex,format, args);
        }

        public void Info(Exception ex, string format, params object[] args)
        {
            log("INFO", ex, format, args);
        }

        public void Debug(Exception ex, string format, params object[] args)
        {
            log("DEBUG", ex, format, args);

        }

        public void Trace(Exception ex, string format, params object[] args)
        {
            log("TRACE", ex, format, args);

        }

        private void log(string level, string format, params object[] args)
        {
            if(level!="DEBUG")
                return;
            
            Console.WriteLine("[{0:00},{1:HH:mm:ss.fff},{2}] {3} {4}",
                                 Thread.CurrentThread.ManagedThreadId,
                                 DateTime.UtcNow,
                                 level,
                                 m_Name,
                                 args.Length == 0 ? format : string.Format(format, args));
        }

        private void log(string level, Exception ex, string format, params object[] args)
        {
            if (level != "DEBUG")
                return;

            var sb = new StringBuilder();
            while (ex != null)
            {
                sb.AppendLine();
                sb.AppendLine(ex.ToString());
                ex = ex.InnerException;
            }

            Console.WriteLine("[{0:00},{1:HH:mm:ss.fff},{2}] {3} {4}\nEXCEPTION(S):{5}",
                                 Thread.CurrentThread.ManagedThreadId,
                                 DateTime.UtcNow,
                                 level,
                                 m_Name,
                                 args.Length == 0 ? format : string.Format(format, args),
                                 sb);
        }
    }
}