using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Inceptum.Raft.Http
{
    internal class RaftRouteHandler : DelegatingHandler
    {
        private string m_RouteName;
        private HttpTransport m_HttpTransport;

        public RaftRouteHandler(string routeName, HttpTransport httpTransport)
        {
            m_HttpTransport = httpTransport;
            m_RouteName = routeName;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Properties["RaftRoute"] = m_RouteName;
            request.Properties["RaftTransport"] = m_HttpTransport;
            return base.SendAsync(request, cancellationToken);
        }
    }
}