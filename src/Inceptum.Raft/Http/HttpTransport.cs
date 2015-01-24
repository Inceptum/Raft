using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Dispatcher;
using System.Web.Http.Routing;
using System.Web.Http.SelfHost;

namespace Inceptum.Raft.Http
{
    public class HttpTransport:ITransport
    {
        readonly Dictionary<Tuple<string, Type>, Action<object>> m_Subscriptions = new Dictionary<Tuple<string, Type>, Action<object>>();

        public void Accept<T>(T message)
        {
            Action<object>[] handlers;
            lock (m_Subscriptions)
            {
                handlers = m_Subscriptions.Where(p => p.Key.Item2 == typeof (T)).Select(p => p.Value).ToArray();
            }
            foreach (var handler in handlers)
            {
                //TODO: exception handling
                handler(message);
            }
        }

        public void Send<T>(string @from, string to, T message)
        {
            if (@from == to)
            {
                Accept(message);
                return;
            }
/*
            HttpClient c = new HttpClient();
            c.SendAsync()
*/
        }

        public IDisposable Subscribe<T>(string subscriberId, Action<T> handler)
        {
            var key = Tuple.Create(subscriberId, typeof(T));
            lock (m_Subscriptions)
            {
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


        public IDisposable RunHost(string baseUrl)
        {
            var config = new HttpSelfHostConfiguration(baseUrl);
            var server = new HttpSelfHostServer(ConfigureHost(config));
            server.OpenAsync().Wait();
            return ActionDisposable.Create(() => server.CloseAsync().Wait());
        }

        public TConfiguration ConfigureHost<TConfiguration>(TConfiguration config)
            where TConfiguration:HttpConfiguration
        {
            config.Properties[typeof (HttpTransport)] = this;
            var controllerSelector = config.Services.GetService(typeof(IHttpControllerSelector)) as IHttpControllerSelector;
            config.Services.Replace(typeof(IHttpControllerSelector), new RaftControllerSelector(config, controllerSelector));


            config.Routes.MapHttpRoute("RESTVoteRequest", "raft/{action}",
               new { controller = "Raft" },
               new { },
               new RaftRouteHandler { InnerHandler = new HttpControllerDispatcher(config) }
               );
 
            return config;
        }
    }
}