using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Inceptum.Raft.Rpc;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Raft.Tests
{
    [TestFixture]
    public class RequestVoteTests
    {
        [Test(Description = "Reply false if term < currentTerm")]
        public void ReplyFalseIfTermIsOlderTest()
        {
            var persistentState = new PersistentState<int> { CurrentTerm = 2 };
            var voteRequest = new VoteRequest
            {
                CandidateId = "nodeA",
                LastLogIndex = -1,
                LastLogTerm = 0,
                Term = 1
            };
            var responses = createFollowerAndHandleVoteRequest(persistentState, voteRequest);
            Assert.That(responses.First().VoteGranted,Is.False,"Vote was granted for request with older term");

        }

        [Test(Description = "If votedFor is null or candidateId, and candidate’s log is at least as up-to-date as receiver’s log, grant vote (§5.2, §5.4)")]
        public void DoNotVoteForCandidatesWithOutdatedLogTest()
        {
            var persistentState = new PersistentState<int> { CurrentTerm = 1 };
            persistentState.Append(new [] {new LogEntry<int>(1, 1)});
            var voteRequest = new VoteRequest
            {
                CandidateId = "nodeA",
                LastLogIndex = -1,
                LastLogTerm = 0,
                Term = 1
            };
            var responses = createFollowerAndHandleVoteRequest(persistentState, voteRequest);
            Assert.That(responses.First().VoteGranted, Is.False, "Vote was granted for request from candidate with oudated log");

        }

        [Test(Description = "If votedFor is null or candidateId, and candidate’s log is at least as up-to-date as receiver’s log, grant vote (§5.2, §5.4)")]
        public void SuccessfulVoteTest()
        {
            var persistentState = new PersistentState<int> { CurrentTerm = 1 };
            var voteRequest = new VoteRequest
            {
                CandidateId = "nodeA",
                LastLogIndex = -1,
                LastLogTerm = 0,
                Term = 1
            };
            var responses = createFollowerAndHandleVoteRequest(persistentState, voteRequest);
            Assert.That(responses.First().VoteGranted, Is.True, "Vote was not granted for request");

        }

        [Test(Description = "If votedFor is null or candidateId, and candidate’s log is at least as up-to-date as receiver’s log, grant vote (§5.2, §5.4)")]
        public void VoteOnlyForOneCndidateWithinTermTest()
        {
            var persistentState = new PersistentState<int> { CurrentTerm = 1 };
            var voteRequests = new[]{ new VoteRequest
            {
                CandidateId = "nodeA",
                LastLogIndex = -1,
                LastLogTerm = 0,
                Term = 1
            }, new VoteRequest
            {
                CandidateId = "nodeB",
                LastLogIndex = -1,
                LastLogTerm = 0,
                Term = 1
            }};
            var responses = createFollowerAndHandleVoteRequest(persistentState, voteRequests).ToArray();
            Assert.That(responses[0].VoteGranted, Is.True, "Vote was not granted for request");
            Assert.That(responses[1].VoteGranted, Is.False, "Vote was granted for two candidates within single term");

        }

        private static IEnumerable<VoteResponse> createFollowerAndHandleVoteRequest(PersistentState<int> persistentState,params VoteRequest[] voteRequests)
        {
            var nodeConfiguration = new NodeConfiguration("testedNode", "nodaA", "nodaB") { ElectionTimeout = 100000 };
            var stateMachine = MockRepository.GenerateMock<IStateMachine<int>>();
            VoteResponse response = null;
            var responseSent = new AutoResetEvent(false);
            Action<string, string, VoteResponse> send = (from, to, r) => { response = r; responseSent.Set(); };
            var transport = mockTransport();
            transport.Expect(t => t.Send(Arg<string>.Is.Equal("testedNode"), Arg<string>.Is.Anything, Arg<VoteResponse>.Is.Anything)).Do(send).Repeat.Times(voteRequests.Count());

            using (var node = new Node<int>(persistentState, nodeConfiguration, transport, stateMachine))
            {
                node.Start();
                foreach (var request in voteRequests)
                {
                    node.Handle(request);
                    Assert.That(responseSent.WaitOne(1000), Is.True, "Response was not sent");
                    yield return response;
                }
            }
        }

        private static ITransport mockTransport()
        {
            var transport = MockRepository.GenerateMock<ITransport>();
            transport.Expect(t => t.Subscribe<VoteRequest>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            transport.Expect(t => t.Subscribe<VoteResponse>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            transport.Expect(t => t.Subscribe<AppendEntriesRequest<int>>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            transport.Expect(t => t.Subscribe<AppendEntriesResponse>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            return transport;
        }
    }
}