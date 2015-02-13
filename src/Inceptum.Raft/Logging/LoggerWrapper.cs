using System;

namespace Inceptum.Raft.Logging
{
    internal class LoggerWrapper:ILogger
    {
        private readonly ILogger m_Logger;
        private readonly Node m_Node;

        public LoggerWrapper(Node node,ILogger logger )
        {
            m_Node = node;
            m_Logger = logger;
        }

        public void Fatal(string format, params object[] args)
        {
            m_Logger.Fatal(getPrefix()+format,args);
        }

        public void Error(string format, params object[] args)
        {
            m_Logger.Error(getPrefix() + format, args);
        }

        public void Info(string format, params object[] args)
        {
            m_Logger.Info(getPrefix() + format, args);
        }

        public void Debug(string format, params object[] args)
        {
            m_Logger.Debug(getPrefix() + format, args);
        }

        public void Trace(string format, params object[] args)
        {
            m_Logger.Trace(getPrefix() + format, args);
        }

        public void Fatal(Exception ex, string format, params object[] args)
        {
            m_Logger.Fatal(ex, getPrefix() + format, args);
        }

        public void Error(Exception ex, string format, params object[] args)
        {
            m_Logger.Error(ex, getPrefix() + format, args);
        }

        public void Info(Exception ex, string format, params object[] args)
        {
            m_Logger.Info(ex, getPrefix() + format, args);
        }

        public void Debug(Exception ex, string format, params object[] args)
        {
            m_Logger.Debug(ex, getPrefix() + format, args);
        }

        public void Trace(Exception ex, string format, params object[] args)
        {
            m_Logger.Trace(ex, getPrefix() + format, args);
        }

        private string getPrefix()
        {
            return string.Format("<{0},{1,-10},{2}> ", m_Node.Id, m_Node.State, m_Node.CurrentTerm);
        }
    }
}