﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inceptum.Raft.Rpc;
using NUnit.Framework;
using Rhino.Mocks;

namespace Inceptum.Raft.Tests
{
    [TestFixture]
    public class ElectionLogicTests
    {
        [Test(Description = "Reply false if term < currentTerm")]
        public void ReplyFalseIfTermIsOlderTest()
        {
            var persistentState = new InMemoryPersistentState { CurrentTerm = 2 };
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
            var persistentState = new InMemoryPersistentState { CurrentTerm = 1 };
            persistentState.Append(new [] {new LogEntry(1, 1)});
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
            var persistentState = new InMemoryPersistentState { CurrentTerm = 1 };
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
        public void VoteOnlyForOneCandidateWithinTermTest()
        {
            var persistentState = new InMemoryPersistentState { CurrentTerm = 1 };
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


        [Test(Description = "On conversion to candidate, start election:Increment currentTerm, Vote for self, Send RequestVote RPCs to all other servers")]
        public void FollowerSwitchToCandidateTest()
        {
            var persistentState = new InMemoryPersistentState { CurrentTerm = 1 };
            var nodeConfiguration = new NodeConfiguration("testedNode", "nodeA", "nodeB") {ElectionTimeout = 100000};
            var bus = mockTransport();
            bus.Expect(t => t.Send(Arg<string>.Is.Equal("testedNode"), Arg<string>.Is.Equal("nodeA"), Arg<VoteRequest>.Is.Anything)).Repeat.Once();
            bus.Expect(t => t.Send(Arg<string>.Is.Equal("testedNode"), Arg<string>.Is.Equal("nodeB"), Arg<VoteRequest>.Is.Anything)).Repeat.Once();
            using (var node = new Node(persistentState, nodeConfiguration, new InMemoryTransport("testedNode", bus), () => new object()))
            {
                node.Start();
                node.SwitchToCandidate();
                bus.VerifyAllExpectations();
                Assert.That(node.CurrentTerm,Is.EqualTo(2),"Term was not incremented");
            }
        }

        [Test(Description = "If votes received from majority of servers: become leader")]
        public void CandidateSwitchToLeaderTest()
        {
            var persistentState = new InMemoryPersistentState { CurrentTerm = 1 };
            var nodeConfiguration = new NodeConfiguration("testedNode", "nodeA", "nodeB") {ElectionTimeout = 100000};
            var bus = mockTransport();
            using (var node = new Node(persistentState, nodeConfiguration, new InMemoryTransport("testedNode", bus), () => new object()))
            {
                node.Start();
                node.SwitchToCandidate();
                node.Handle(new VoteResponse{NodeId = "nodeA",Term = 2,VoteGranted = true});
                Assert.That(node.State, Is.EqualTo(NodeState.Leader), "candidate has not converted to leader after received votes from majority ");
            }
        }

        [Test(Description = "If AppendEntries RPC received from new leader: candidate converts to follower")]
        public void SwitchToFollowerFromCandidateOnAppendEntriesTest()
        {
            var persistentState = new InMemoryPersistentState { CurrentTerm = 1 };
            var nodeConfiguration = new NodeConfiguration("testedNode", "nodeA", "nodeB") {ElectionTimeout = 100000};
            var bus = mockTransport();
            using (var node = new Node(persistentState, nodeConfiguration, new InMemoryTransport("testedNode", bus), () => new object()))
            {
                node.Start();
                node.SwitchToCandidate();
                var appendEntriesRequest = new AppendEntriesRequest { Entries = new LogEntry[0], LeaderCommit = -1, LeaderId = "nodeA", PrevLogIndex = -1, PrevLogTerm = -1, Term = 2 };

                node.Handle(appendEntriesRequest);
                Assert.That(node.CurrentTerm,Is.EqualTo(2),"Term was not incremented");
                Assert.That(node.State, Is.EqualTo(NodeState.Follower), "candidate has not converted to follower on AppendEntriesfrom leader");
                Assert.That(node.LeaderId, Is.EqualTo("nodeA"), "LeaderId is not set");
            }
        }

        [Test(Description = "If election timeout elapses: candidate starts new election")]
        public void IfElectionTimeoutElapsesCandidateStartsNewElectionTest()
        {
            var persistentState = new InMemoryPersistentState { CurrentTerm = 1 };
            var nodeConfiguration = new NodeConfiguration("testedNode", "nodeA", "nodeB") {ElectionTimeout = 100};
            var bus = mockTransport();
            using (var node = new Node(persistentState, nodeConfiguration, new InMemoryTransport("testedNode", bus), () => new object()))
            {
                node.Start();
                node.SwitchToCandidate();
                Thread.Sleep(200);
                Assert.That(node.CurrentTerm,Is.EqualTo(3),"Election was not started");
            }
        }

        [Test(Description = "If election timeout elapses without receiving AppendEntries RPC from current leader or granting vote to candidate: convert to candidate")]
        public void IfElectionTimeoutElapsesFollowerConvertsToCandidateTest()
        {
            var persistentState = new InMemoryPersistentState { CurrentTerm = 1 };
            var nodeConfiguration = new NodeConfiguration("testedNode", "nodeA", "nodeB") {ElectionTimeout = 100};
            var bus = mockTransport();
            using (var node = new Node(persistentState, nodeConfiguration, new InMemoryTransport("testedNode", bus), () => new object()))
            {
                node.Start();
                Thread.Sleep(200);
                Assert.That(node.State, Is.EqualTo(NodeState.Candidate), "Follower has not converted to candidate");
            }
        }

        private static IEnumerable<VoteResponse> createFollowerAndHandleVoteRequest(InMemoryPersistentState persistentState,params VoteRequest[] voteRequests)
        {
            var nodeConfiguration = new NodeConfiguration("testedNode", "nodeA", "nodeB") { ElectionTimeout = 100000 };
            VoteResponse response = null;
            var responseSent = new AutoResetEvent(false);
            Func<string, string, VoteResponse,Task<Object>> send = (from, to, r) => { response = r; responseSent.Set(); return Task.FromResult(default(object)); };
            var bus = mockTransport();
            bus.Expect(t => t.Send(Arg<string>.Is.Equal("testedNode"), Arg<string>.Is.Anything, Arg<VoteResponse>.Is.Anything)).Do(send).Repeat.Times(voteRequests.Count());

            using (var node = new Node(persistentState, nodeConfiguration, new InMemoryTransport("testedNode", bus), () => new object()))
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


        private static IInMemoryBus mockTransport()
        {
            var transport = MockRepository.GenerateMock<IInMemoryBus>();
            transport.Expect(t => t.Subscribe<VoteRequest>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            transport.Expect(t => t.Subscribe<VoteResponse>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            transport.Expect(t => t.Subscribe<AppendEntriesRequest>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            transport.Expect(t => t.Subscribe<AppendEntriesResponse>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            transport.Expect(t => t.Subscribe<ApplyCommadRequest>(null, null)).IgnoreArguments().Return(ActionDisposable.Create(() => { }));
            return transport;
        }
    }
}