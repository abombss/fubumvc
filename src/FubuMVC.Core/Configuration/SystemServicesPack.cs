using FubuCore.Descriptions;
using FubuMVC.Core.Caching;
using FubuMVC.Core.Http;
using FubuMVC.Core.Registration;
using FubuMVC.Core.Registration.Services;
using FubuMVC.Core.Runtime.Files;
using FubuMVC.Core.Security;

namespace FubuMVC.Core.Configuration
{
    public class SystemServicesPack : ConfigurationPack
    {
        public SystemServicesPack()
        {
            For(ConfigurationType.Explicit);

            Services<ModelBindingServicesRegistry>();
            Services<SecurityServicesRegistry>();
            Services<HttpStandInServiceRegistry>();
            Services<CoreServiceRegistry>();
            Services<CachingServiceRegistry>();
        }

    }
}