using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Inceptum.Raft.Http
{
    internal class RaftRouteHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Properties["RaftRoute"] = true;
            return base.SendAsync(request, cancellationToken);
        }
    }
}