using System;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Dispatcher;
using System.Web.Http.Routing;
using System.Web.Http.SelfHost;

namespace Inceptum.Raft.Http
{
    public class HttpTransport:ITransport
    {
        public void Send<T>(string @from, string to, T message)
        {
/*
            HttpClient c = new HttpClient();
            c.SendAsync()
*/
        }

        public IDisposable Subscribe<T>(string subscriberId, Action<T> handler)
        {
           // throw new NotImplementedException();
            return ActionDisposable.Create(() => { });
        }


        public IDisposable RunHost(string baseUrl)
        {
            var config = new HttpSelfHostConfiguration(baseUrl);
            var server = new HttpSelfHostServer(configureHost(config));
            server.OpenAsync().Wait();
            return ActionDisposable.Create(() => server.CloseAsync().Wait());
        }

        public HttpSelfHostConfiguration configureHost(HttpSelfHostConfiguration config)
        {
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