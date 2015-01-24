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
        readonly Dictionary< Type , Action<object>> m_Subscriptions = new Dictionary<Type, Action<object>>();

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

        public void Send<T>( string to, T message)
        {
            
/*
            HttpClient c = new HttpClient();
            c.SendAsync()
*/
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