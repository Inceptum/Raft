using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Inceptum.Raft
{
    public class FilePersistentState<TCommand> : PersistentStateBase<TCommand>,IDisposable
    {
        readonly Dictionary<long,long> m_Map=new Dictionary<long, long>();
        private readonly BinaryFormatter m_Formatter;
        private readonly FileStream m_LogFileStream;
        private readonly FileStream m_StateFileStream;

        public FilePersistentState(string path)
        {
            var diretory = Path.GetFullPath(path);
            if (Directory.Exists(diretory))
                Directory.CreateDirectory(diretory);
            m_Formatter = new BinaryFormatter();
            var logFile = Path.Combine(diretory,"log.data");
            var stateFile = Path.Combine(diretory,"state.data");
            m_LogFileStream = File.Open(logFile,FileMode.OpenOrCreate,FileAccess.ReadWrite, FileShare.None);
            m_StateFileStream = File.Open(stateFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            if (m_StateFileStream.Length == 0)
            {
                SaveState(0,null);
            }
            while (m_LogFileStream.Position<m_LogFileStream.Length)
            {
                var position = m_LogFileStream.Position;
                m_Formatter.Deserialize(m_LogFileStream);
                m_Map.Add(m_Map.Count, position);
            }
            Init();
        }

        protected override Tuple<long, string> LoadState()
        {
            m_StateFileStream.Seek(0, SeekOrigin.Begin);
            return (Tuple<long, string>)m_Formatter.Deserialize(m_StateFileStream);
        }

        protected override sealed void SaveState(long currentTerm, string votedFor)
        {
            m_StateFileStream.SetLength(0);
            m_Formatter.Serialize(m_StateFileStream, Tuple.Create(currentTerm, votedFor));
            m_StateFileStream.Flush();
        }

        protected override void AppendLog(params LogEntry<TCommand>[] logEntries)
        {
            m_LogFileStream.Seek(0, SeekOrigin.End);
            foreach (var logEntry in logEntries)
            {
                var position = m_LogFileStream.Length;
                m_Formatter.Serialize(m_LogFileStream, logEntry);
                m_Map.Add(m_Map.Count, position);
            }
            m_LogFileStream.Flush();
        }

        protected override IEnumerable<LogEntry<TCommand>> LoadLog()
        {
            try
            {
                m_LogFileStream.Seek(0, SeekOrigin.Begin);
                while (m_LogFileStream.Position != m_LogFileStream.Length)
                {
                    yield return (LogEntry<TCommand>) m_Formatter.Deserialize(m_LogFileStream);
                }
            }
            finally
            {
                m_LogFileStream.Seek(0, SeekOrigin.End);
            }
        }

        protected override void RemoveLogStartingFrom(int index)
        {
            m_LogFileStream.SetLength(m_Map[index]);
            m_LogFileStream.Flush();
        }

        public void Dispose()
        {
            m_LogFileStream.Dispose();
            m_StateFileStream.Dispose();
        }
    }
}