using System.Web.Http;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft.Http
{
    internal class RaftController:ApiController
    {
        [HttpGet]
        public IHttpActionResult VoteRequest([FromUri]VoteRequest voteRequest)
        {
            return Ok(voteRequest);
        }

        [HttpGet]
        public IHttpActionResult VoteResponse([FromUri]VoteResponse voteResponse)
        {
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
            return Ok(appendEntriesResponse);
        }
    }
}