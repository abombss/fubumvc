using System.Web.Routing;
using FubuMVC.Core.Registration;

namespace FubuMVC.Core.Http
{
    public class HttpStandInServiceRegistry : ServiceRegistry
    {
        public HttpStandInServiceRegistry()
        {
            SetServiceIfNone<ICurrentHttpRequest, StandInCurrentHttpRequest>();

            SetServiceIfNone<IHttpResponse, NulloHttpResponse>();

            SetServiceIfNone<ICurrentChain>(new CurrentChain(null, null));

            SetServiceIfNone(new RouteData());
        }
    }
}