using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Inceptum.Raft
{
    public class StatePersister<TCommand> : IStatePersister<TCommand>,IDisposable
    {
        readonly Dictionary<long,long> m_Map=new Dictionary<long, long>();
        private readonly BinaryFormatter m_Formatter;
        private readonly FileStream m_LogFileStream;
        private readonly FileStream m_StateFileStream;

        public StatePersister(string path)
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
                m_Formatter.Deserialize(m_LogFileStream);
                m_Map.Add(m_Map.Count, m_LogFileStream.Position);
            }
        }

        public Tuple<long, string> GetState()
        {
            m_StateFileStream.Seek(0, SeekOrigin.Begin);
            return (Tuple<long, string>)m_Formatter.Deserialize(m_StateFileStream);
        }

        public void SaveState(long term, string votedFor)
        {
            m_StateFileStream.SetLength(0);
            m_Formatter.Serialize(m_StateFileStream, Tuple.Create(term,votedFor));
            m_StateFileStream.Flush();
        }

        public void Append(params ILogEntry<TCommand>[] entries)
        {
            foreach (var logEntry in entries)
            {
                m_Formatter.Serialize(m_LogFileStream, logEntry);   
                m_Map.Add(m_Map.Count,m_LogFileStream.Position);
            }
            m_LogFileStream.Flush();
        }

        public IEnumerable<ILogEntry<TCommand>> GetLog()
        {
            try
            {
                m_LogFileStream.Seek(0, SeekOrigin.Begin);
                while (m_LogFileStream.Position != m_LogFileStream.Length)
                {
                    yield return (ILogEntry<TCommand>) m_Formatter.Deserialize(m_LogFileStream);
                }
            }
            finally
            {
                m_LogFileStream.Seek(0, SeekOrigin.End);
            }
        }

        public void RemoveAfter(int index)
        {
            m_LogFileStream.SetLength(m_Map[index]);
        }

        public void Dispose()
        {
            m_LogFileStream.Dispose();
            m_StateFileStream.Dispose();
        }
    }
}