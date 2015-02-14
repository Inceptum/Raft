using System;
using System.Net.Http;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft.Http
{
    internal class RaftController : ApiController
    {
        private HttpTransport m_Transport;
     
        public override Task<HttpResponseMessage> ExecuteAsync(HttpControllerContext controllerContext, CancellationToken cancellationToken)
        {
            m_Transport = (HttpTransport)controllerContext.Request.Properties["RaftTransport"];
            return base.ExecuteAsync(controllerContext, cancellationToken);
        }

        [HttpGet]
        public IHttpActionResult VoteRequest([FromUri]VoteRequest voteRequest)
        {
            m_Transport.Accept(voteRequest);
            return Ok();
        }

        [HttpGet]
        public IHttpActionResult VoteResponse([FromUri]VoteResponse voteResponse)
        {
            m_Transport.Accept(voteResponse);
            return Ok();
        } 
        
        [HttpPost]
        public async Task<IHttpActionResult> AppendEntriesRequest([FromUri]AppendEntriesRequest appendEntriesRequest)
        {
            var formatter = new BinaryFormatter();
            var stream = await Request.Content.ReadAsStreamAsync();
            var logEntries = formatter.Deserialize(stream) as LogEntry[];
            appendEntriesRequest.Entries = logEntries;
            m_Transport.Accept(appendEntriesRequest);

            return Ok();
        } 
        [HttpGet]
        public IHttpActionResult AppendEntriesResponse([FromUri]AppendEntriesResponse appendEntriesResponse)
        {
            m_Transport.Accept(appendEntriesResponse);
            return Ok();
        }        
        
        [HttpPost]
        public async Task<IHttpActionResult> Command()
        {
            var formatter = new BinaryFormatter();
            var stream = await Request.Content.ReadAsStreamAsync();
            var command = formatter.Deserialize(stream);
            try
            {
                await m_Transport.Accept(new ApplyCommadRequest { Command = command });
            }
            catch (Exception e)
            {
                return InternalServerError(e);
            }
            return Ok();
        }
    }
}