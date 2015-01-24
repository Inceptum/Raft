using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft.Http
{
    internal class RaftController:ApiController
    {
        private HttpTransport m_Transport;

     
        public override Task<HttpResponseMessage> ExecuteAsync(HttpControllerContext controllerContext, CancellationToken cancellationToken)
        {
            m_Transport = (HttpTransport)controllerContext.Configuration.Properties[typeof(HttpTransport)];

            return base.ExecuteAsync(controllerContext, cancellationToken);
        }

        [HttpGet]
        public IHttpActionResult VoteRequest([FromUri]VoteRequest voteRequest)
        {
            m_Transport.Accept(voteRequest);

            return Ok(voteRequest);
        }

        [HttpGet]
        public IHttpActionResult VoteResponse([FromUri]VoteResponse voteResponse)
        {
            m_Transport.Accept(voteResponse);
            return Ok(voteResponse);
        } 
        
       /* [HttpPost]
        public IHttpActionResult AppendEntriesRequest([FromUri]AppendEntriesRequest<> appendEntriesRequest)
        {
            return Ok(string.Format(@"{0}<br>{1}<br>{2}<br>{3}<br>{4}", appendEntriesRequest.Term, appendEntriesRequest.LeaderId, appendEntriesRequest.PrevLogIndex, appendEntriesRequest.PrevLogTerm, appendEntriesRequest.LeaderCommit));
        }*/
        [HttpGet]
        public IHttpActionResult AppendEntriesResponse([FromUri]AppendEntriesResponse appendEntriesResponse)
        {
            m_Transport.Accept(appendEntriesResponse);
            return Ok(appendEntriesResponse);
        }
    }
}