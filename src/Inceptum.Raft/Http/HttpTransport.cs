using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Dispatcher;
using System.Web.Http.SelfHost;
using Inceptum.Raft.Rpc;

namespace Inceptum.Raft.Http
{
    public class HttpTransport:ITransport,IDisposable
    {
        readonly Dictionary< Type , Func<object,Task<object>>> m_Subscriptions = new Dictionary<Type, Func<object, Task<object>>>();
        private readonly Dictionary<string, Uri> m_Endpoints;
        private readonly ConcurrentDictionary<Uri, ConcurrentQueue<HttpClient>> m_ClientsCache = new ConcurrentDictionary<Uri, ConcurrentQueue<HttpClient>>();


        public HttpTransport(Dictionary<string,Uri> endpoints )
        {
            m_Endpoints = endpoints;
        }

        internal async Task<object> Accept<T>(T message)
        {
            Func<object, Task<object>> handler;
            var key = typeof (T);
            lock (m_Subscriptions)
            {
                if (!m_Subscriptions.TryGetValue(key,out handler))
                    return null;
            }

            //TODO: exception handling
            await handler(message);
            return null;
        }

        public IDisposable RunHost(string baseUrl)
        {
            var config = new HttpSelfHostConfiguration(baseUrl);
            var server = new HttpSelfHostServer(ConfigureHost(config));
            server.OpenAsync().Wait();
            return ActionDisposable.Create(() => server.CloseAsync().Wait());
        }

        public TConfiguration ConfigureHost<TConfiguration>(TConfiguration config,string urlPrefix=null)
            where TConfiguration:HttpConfiguration
        {
            var routeName = "Raft" + (urlPrefix??"");
            var controllerSelector = config.Services.GetService(typeof(IHttpControllerSelector)) as IHttpControllerSelector;
            config.Services.Replace(typeof(IHttpControllerSelector), new RaftControllerSelector(config, controllerSelector, routeName));


            config.Routes.MapHttpRoute(routeName, urlPrefix!=null  ? urlPrefix + "/raft/{action}" : "raft/{action}",
                new { controller = "Raft" },
                new { },
                new RaftRouteHandler(routeName, this) { InnerHandler = new HttpControllerDispatcher(config) }
                );
 
            return config;
        }

        public IDisposable Subscribe<T>(Func<T, Task<object>> handler)
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

        public void Send(string to, AppendEntriesRequest message)
        {
            Uri baseUri;
            if (!m_Endpoints.TryGetValue(to, out baseUri))
                throw new InvalidOperationException(string.Format("Uri for node {0} is not provided", to));
            var requestUri = string.Format("raft/AppendEntriesRequest?Term={0}&LeaderId={1}&PrevLogIndex={2}&PrevLogTerm={3}&LeaderCommit={4}", message.Term, message.LeaderId, message.PrevLogIndex, message.PrevLogTerm, message.LeaderCommit);

            send(baseUri, client => client.PostAsync(requestUri, new AppendEntriesRequestContent(message.Entries)));
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
            var requestUri = string.Format("raft/VoteRequest?Term={0}&CandidateId={1}&LastLogIndex={2}&LastLogTerm={3}", message.Term, message.CandidateId, message.LastLogIndex, message.LastLogTerm);
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

        public async Task<object> Send(string to, ApplyCommadRequest message)
        {
            Uri baseUri;
            if (!m_Endpoints.TryGetValue(to, out baseUri))
                throw new InvalidOperationException(string.Format("Uri for node {0} is not provided", to));
            var requestUri = string.Format("raft/Command");
            await send(baseUri, async client => await client.PostAsync(requestUri, new ApplyCommandContent(message.Command)));
            return Task.FromResult<object>(null);
        }


        private async Task<HttpResponseMessage> send(Uri baseUri, Func<HttpClient, Task<HttpResponseMessage>> sendRequest)
        {
            var queue = m_ClientsCache.GetOrAdd(baseUri, _ => new ConcurrentQueue<HttpClient>());
            HttpClient client;
            if (queue.TryDequeue(out client) == false)
            {
                client = new HttpClient
                {
                    BaseAddress = baseUri
                };
            }
            HttpResponseMessage response=await sendRequest(client);
           
            if (response.StatusCode != HttpStatusCode.OK)
            {
                //TODO: log
            }
            queue.Enqueue(client);
            return response;
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