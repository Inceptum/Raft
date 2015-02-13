using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.SelfHost;
using Inceptum.Raft;
using Inceptum.Raft.Http;

namespace TestConsoleApplication
{
    class StateMachine
    {
        private static bool hasFailed = false;
        public int Value { get; set; }
        public string Node { get; set; }

        public StateMachine(string node)
        {
            Node = node;
        }

        public void Apply(int command)
        {
            if (Node == "node1" && command == 3 && !hasFailed)
            {
                hasFailed = true;
                Console.WriteLine("{0}:oops!", Node);
                throw new Exception("oops");
            }
            // Thread.Sleep(1000);
            Value +=  command;
            Console.WriteLine("{0}:applying  {1}. New Value is {2}", Node, command, Value);
        }
    }
    class Program
    {
        private static  PerformanceCounter m_CpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

        private static void Main(string[] args)
        {
            var knownNodes = Enumerable.Range(1, 3).Select(i => "node" + i);

            string baseUrl = string.Format(@"{0}://localhost:{1}", "http", 9222);
            var transports = knownNodes.ToDictionary(n => n, n => new HttpTransport(knownNodes.ToDictionary(kn => kn, kn => new Uri(string.Format("{0}/{1}/", baseUrl, kn)))));

            var readLine = Console.ReadLine();
            int number;
            bool needPrefix = true;
            if (int.TryParse(readLine, out number))
            {
                Console.Title = "node"+number;
                needPrefix = false;
                baseUrl = string.Format(@"{0}://localhost:{1}", "http", 9000+number);
                transports = new Dictionary<string, HttpTransport> { { "node" + number, new HttpTransport(
                    Enumerable.Range(1, 3).ToDictionary(kn => "node"+kn, kn => new Uri(string.Format("http://localhost:{0}/",9000+ kn)))) } };
            }
            //   Test(150);



            var nodes = transports.Select(
                   p => new Node(
                           new InMemoryPersistentState(),
                           new NodeConfiguration(p.Key, knownNodes.ToArray()) { ElectionTimeout = 300 },
                           p.Value,
                           () =>
                           {
                               string key = p.Key;
                               return new StateMachine(key);
                           }))
                   .ToArray();

            Console.WriteLine(baseUrl);
            var config = new HttpSelfHostConfiguration(baseUrl);
            foreach (var t in transports)
            {
                t.Value.ConfigureHost(config, needPrefix?t.Key:null);
            }
            var server = new HttpSelfHostServer(config);
            server.OpenAsync().Wait();

            foreach (var node in nodes)
            {
                node.Start();
            }

            string input = null;
            while (input  != "exit")
            {
                input=Console.ReadLine();
                int command;
                if (int.TryParse(input, out command))
                {
                    try
                    {
                        nodes.First().Apply(command);
                    }
                    catch (Exception ex)
                    {

                        Console.WriteLine("Command failed: "+ex.ToString());
                    }
                    
                    Console.WriteLine("Applied {0}",command);
                }
            }
            foreach (var node in nodes)
            {
                node.Dispose();
            }

            server.CloseAsync().Wait();

        }

        private static void Test(int electionTimeout)
        {
            var sb = new StringBuilder();
            try
            {
                var knownNodes = Enumerable.Range(1, 5).Select(i => "node" + i);

                var bus = new InMemoryBus();

                var nodes = knownNodes.Select(
                    id =>new Node(
                            new InMemoryPersistentState(), 
                            new NodeConfiguration(id, knownNodes.ToArray()) { ElectionTimeout = electionTimeout },
                            new InMemoryTransport(id,bus),
                            () =>
                            {
                                string s = id;
                                return new StateMachine(s);
                            }))
                    .ToArray();

                var start = DateTime.Now;
                foreach (var node in nodes)
                {
                    node.Start();
                }

                var tokenSource2 = new CancellationTokenSource();
                CancellationToken ct = tokenSource2.Token;

                StringBuilder log=new StringBuilder();
                Task.Factory.StartNew(() =>
                {
                    long cterm = 0;
                    while (true)
                    {
                        var nodeStates = nodes.Select(node => new {node.Id, node.State, node.LeaderId, node.Configuration, node.CurrentTerm}).ToArray();

                        Console.Write(log.ToString());
                        Console.WriteLine("[{3:000}] {0} - {1} ({2}) CPU: {4:00}%", start, DateTime.Now, DateTime.Now - start, cterm,m_CpuCounter.NextValue());
                        foreach (var node in nodeStates)
                        {
                            if (node.CurrentTerm > cterm)
                            {
                                log.AppendLine(string.Format("[{3:000}] {0} - {1} ({2}) CPU: {4:00}%", start, DateTime.Now, DateTime.Now - start, cterm, m_CpuCounter.NextValue()));
                                cterm = node.CurrentTerm;
                                start = DateTime.Now;
                            }
                            Console.WriteLine("[{3}] {0}: {1}\tLeader:{2}", node.Id, node.State, node.LeaderId, node.CurrentTerm);
                        }
                        Console.WriteLine();
                        Thread.Sleep(500);
                    }
                },ct);

                Console.ReadLine();
                var leaderId = nodes.First().LeaderId;
                Console.WriteLine("Failing the leader" + leaderId);
               bus.EmulateConnectivityIssue(leaderId);

               Console.ReadLine();
               Console.WriteLine("Restoring ex leader" + leaderId);
               bus.RestoreConnectivity(leaderId);
               Thread.Sleep(1000);

               Console.ReadLine();
                tokenSource2.Cancel();

                {
                    var nodeStates = nodes.Select(node => new {node.Id, node.State, node.LeaderId, node.Configuration}).ToArray();
                    foreach (var node in nodes)
                    {
                        node.Dispose();
                    }
                    foreach (var node in nodeStates)
                    {
                        sb.AppendLine(string.Format("{0}: {1}\tLeader:{2}", node.Id, node.State, node.LeaderId));
                        foreach (var knownNode in node.Configuration.KnownNodes)
                        {
                            sb.AppendLine(string.Format("\t{0}", knownNode));
                        }
                    }
                }

                var term = nodes.Select(n => n.CurrentTerm).First();
                Debug.WriteLine("{0}\t{1}", (nodes.Single(n => n.State == NodeState.Leader).CurrentStateEnterTime - start).TotalMilliseconds.ToString().Replace(".", ","), term);
            }
            catch
            {
                Console.WriteLine();
                Console.WriteLine(sb.ToString());
                Console.WriteLine();
                Console.WriteLine();

                throw;
            }

            finally
            {
                //Console.WriteLine(Node<object>.m_Log);
            }
            
        }
    }
}
