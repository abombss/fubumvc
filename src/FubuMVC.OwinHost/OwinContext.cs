using System.Collections.Generic;

namespace FubuMVC.OwinHost
{
    public class OwinContext
    {
        private readonly IDictionary<string, object> _environment;

        public OwinContext(IDictionary<string, object> environment)
        {
            _environment = environment;
        }

        public IDictionary<string, object> Environment
        {
            get { return _environment; }
        }
    }
}