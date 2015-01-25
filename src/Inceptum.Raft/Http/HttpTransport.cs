using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Dispatcher;
using System.Web.Http.Routing;
using System.Web.Http.SelfHost;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft.Http
{
    public class AppendEntriesRequestContent<TCommand> : HttpContent
    {
        private readonly LogEntry<TCommand>[] m_Entries;
        private readonly BinaryFormatter m_Formatter = new BinaryFormatter();
        public AppendEntriesRequestContent(IEnumerable<LogEntry<TCommand>> entries)
        {
            m_Entries = entries.ToArray();
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            m_Formatter.Serialize(stream, m_Entries);
            return Task.FromResult(1);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }


    public class HttpTransport:ITransport,IDisposable
    {
        readonly Dictionary< Type , Action<object>> m_Subscriptions = new Dictionary<Type, Action<object>>();
        private readonly Dictionary<string, Uri> m_Endpoints;
        private readonly ConcurrentDictionary<Uri, ConcurrentQueue<HttpClient>> m_ClientsCache = new ConcurrentDictionary<Uri, ConcurrentQueue<HttpClient>>();


        public HttpTransport(Dictionary<string,Uri> endpoints )
        {
            m_Endpoints = endpoints;
        }

        public void Accept<T>(T message)
        {
            Action<object> handler;
            var key = typeof (T);
            lock (m_Subscriptions)
            {
                if (!m_Subscriptions.TryGetValue(key,out handler))
                    return;
            }

            //TODO: exception handling
            handler(message);
        }

        public void Send<TCommand>(string to, AppendEntriesRequest<TCommand> message)
        {
            Uri baseUri;
            if (!m_Endpoints.TryGetValue(to, out baseUri))
                throw new InvalidOperationException(string.Format("Uri for node {0} is not provided", to));
            var requestUri = string.Format("raft/AppendEntriesRequest?Term={0}&LeaderId={1}&PrevLogIndex={2}&PrevLogTerm={3}&LeaderCommit={4}", message.Term, message.LeaderId, message.PrevLogIndex, message.PrevLogTerm, message.LeaderCommit);

            send(baseUri, client => client.PostAsync(requestUri, new AppendEntriesRequestContent<TCommand>(message.Entries)));
        }

        private void send (Uri baseUri, Func<HttpClient,Task<HttpResponseMessage>> sendRequest)
        {
            var queue=m_ClientsCache.GetOrAdd(baseUri, _ => new ConcurrentQueue<HttpClient>());
            HttpClient client;
            if (queue.TryDequeue(out client) == false)
            {
                client = new HttpClient
                {
                    BaseAddress = baseUri
                };
            }
            sendRequest(client).ContinueWith(task =>
            {
                if (task.Result.StatusCode != HttpStatusCode.OK)
                {
                    //TODO: log
                }
                queue.Enqueue(client);
            });

        }

       

        public void Send(string to, AppendEntriesResponse message)
        {
            Uri baseUri;
            if (!m_Endpoints.TryGetValue(to, out baseUri))
                throw new InvalidOperationException(string.Format("Uri for node {0} is not provided", to));
            var requestUri = string.Format("raft/AppendEntriesResponse?Term={0}&Success={1}&NodeId={2}",message.Term,message.Success,message.NodeId);
            send(baseUri, client => client.GetAsync(requestUri));
        }

        public void Send(string to, VoteRequest message)
        {
            Uri baseUri;
            if (!m_Endpoints.TryGetValue(to, out baseUri))
                throw new InvalidOperationException(string.Format("Uri for node {0} is not provided", to));
            var requestUri = string.Format("raft/voteRequest?Term={0}&CandidateId={1}&LastLogIndex={2}&LastLogTerm={3}",message.Term,message.CandidateId,message.LastLogIndex,message.LastLogTerm);
            send(baseUri, client => client.GetAsync(requestUri));

        }

        public void Send(string to, VoteResponse message)
        {
            Uri baseUri;
            if (!m_Endpoints.TryGetValue(to, out baseUri))
                throw new InvalidOperationException(string.Format("Uri for node {0} is not provided", to));
            var requestUri = string.Format("raft/VoteResponse?Term={0}&VoteGranted={1}&NodeId={2}",message.Term,message.VoteGranted,message.NodeId);
            send(baseUri, client => client.GetAsync(requestUri));

        }
 

        public IDisposable Subscribe<T>(  Action<T> handler)
        {
            var key =   typeof(T);
            lock (m_Subscriptions)
            {
                if (m_Subscriptions.ContainsKey(key))
                    throw new InvalidOperationException(string.Format("Handler for {0} is already registered",key));
                m_Subscriptions.Add(key, m => handler((T)m));
            }
            return ActionDisposable.Create(() =>
            {
                lock (m_Subscriptions)
                {
                    m_Subscriptions.Remove(key);
                }
            });
         
        }


        public IDisposable RunHost<TCommand>(string baseUrl)
        {
            var config = new HttpSelfHostConfiguration(baseUrl);
            var server = new HttpSelfHostServer(ConfigureHost<HttpSelfHostConfiguration,TCommand>(config));
            server.OpenAsync().Wait();
            return ActionDisposable.Create(() => server.CloseAsync().Wait());
        }

        public TConfiguration ConfigureHost<TConfiguration, TCommand>(TConfiguration config,string urlPrefix=null)
            where TConfiguration:HttpConfiguration
        {
            var routeName = "Raft" + (urlPrefix??"");
            //config.Properties[typeof (HttpTransport)] = this;
            var controllerSelector = config.Services.GetService(typeof(IHttpControllerSelector)) as IHttpControllerSelector;
            config.Services.Replace(typeof(IHttpControllerSelector), new RaftControllerSelector<TCommand>(config, controllerSelector, routeName));


            config.Routes.MapHttpRoute(routeName, urlPrefix!=null  ? urlPrefix + "/raft/{action}" : "raft/{action}",
               new { controller = "Raft" },
               new { },
               new RaftRouteHandler(routeName, this) { InnerHandler = new HttpControllerDispatcher(config) }
               );
 
            return config;
        }

        public void Dispose()
        {
            foreach (var q in m_ClientsCache.Select(x => x.Value))
            {
                HttpClient result;
                while (q.TryDequeue(out result))
                {
                    result.Dispose();
                }
            }
            m_ClientsCache.Clear();
        }
    }
}