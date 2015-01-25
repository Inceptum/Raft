using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Dispatcher;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Inceptum.Raft.Http
{
    //http://www.strathweb.com/2013/09/dynamic-per-controller-httpconfiguration-asp-net-web-api/

    internal class RaftControllerSelector<TCommand> : IHttpControllerSelector
    {
        private readonly IHttpControllerSelector m_ControllerSelector;
        private readonly HttpControllerDescriptor m_RaftControllerDescriptor;
        private string m_RouteName;

        public RaftControllerSelector(HttpConfiguration configuration, IHttpControllerSelector controllerSelector, string routeName)
        {

            m_ControllerSelector = controllerSelector;
            m_RouteName = routeName;
            m_RaftControllerDescriptor = createRaftControllerDescriptor(configuration);

        }

        private static HttpControllerDescriptor createRaftControllerDescriptor(HttpConfiguration configuration)
        {
            var controllerSettings = new HttpControllerSettings(configuration);
            var jsonFormatter = controllerSettings.Formatters.JsonFormatter;
            jsonFormatter.SerializerSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
            jsonFormatter.SerializerSettings.MissingMemberHandling = MissingMemberHandling.Ignore;
            jsonFormatter.SerializerSettings.Formatting = Formatting.Indented;
            jsonFormatter.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            controllerSettings.Formatters.Remove(controllerSettings.Formatters.XmlFormatter);
            var constructor = typeof(HttpConfiguration).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(HttpConfiguration), typeof(HttpControllerSettings) }, null);
            var config = (HttpConfiguration)constructor.Invoke(new object[] { configuration, controllerSettings });
            return new HttpControllerDescriptor(config, "Raft", typeof(RaftController<TCommand>));
        }

        public HttpControllerDescriptor SelectController(HttpRequestMessage request)
        {
            if (request.Properties.ContainsKey("RaftRoute") && request.Properties["RaftRoute"].ToString() == m_RouteName)
                return m_RaftControllerDescriptor;

            return m_ControllerSelector.SelectController(request);
        }

        public IDictionary<string, HttpControllerDescriptor> GetControllerMapping()
        {
            var descriptors = m_ControllerSelector.GetControllerMapping();
            descriptors.Add("Raft", m_RaftControllerDescriptor);
            return descriptors;
        }
    }
}