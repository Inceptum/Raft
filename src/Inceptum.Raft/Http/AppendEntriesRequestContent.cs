using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

namespace Inceptum.Raft.Http
{
    public class AppendEntriesRequestContent : HttpContent
    {
        private readonly LogEntry[] m_Entries;
        private readonly BinaryFormatter m_Formatter = new BinaryFormatter();
        public AppendEntriesRequestContent(IEnumerable<LogEntry> entries)
        {
            m_Entries = entries.ToArray();
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            //TODO: need something better than just binary serialized data in post content
            m_Formatter.Serialize(stream, m_Entries);
            return Task.FromResult(1);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }
}