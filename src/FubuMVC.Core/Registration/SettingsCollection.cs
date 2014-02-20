using System;
using System.Threading.Tasks;
using FubuCore.Binding;
using FubuCore.Configuration;
using FubuCore.Util;
using FubuCore;
using FubuCore.Reflection;
using FubuMVC.Core.Registration.ObjectGraph;

namespace FubuMVC.Core.Registration
{
    public class SettingsCollection
    {
        private readonly static Lazy<ISettingsProvider> SettingsProvider = new Lazy<ISettingsProvider>(() => new AppSettingsProvider(ObjectResolver.Basic()));

        private readonly SettingsCollection _parent;
        private readonly Cache<Type, object> _settings = new Cache<Type, object>();

        public SettingsCollection(SettingsCollection parent)
        {
            _settings.OnMissing = buildDefault;
            _parent = parent;
        }

        private static object buildDefault(Type type)
        {
            var templateType = type.IsConcreteWithDefaultCtor()
                ? typeof (AppSettingMaker<>)
                : typeof (DefaultMaker<>);

            return templateType.CloseAndBuildAs<IDefaultMaker>(type).MakeDefault();
        }

        public T Get<T>() where T : class
        {
            return getTask<T>().Result;
        }

        private Task<T> getTask<T>()
        {
            return selectSettings<T>()[typeof (T)].As<Task<T>>();
        }

        private Cache<Type, object> selectSettings<T>()
        {
            if (_parent != null && !HasExplicit<T>() && (_parent._settings.Has(typeof(T)) || typeof(T).HasAttribute<ApplicationLevelAttribute>()))
            {
                return _parent._settings;
            }

            return _settings;
        }

        public void Alter<T>(Action<T> alteration) where T : class
        {
            var settings = typeof (T).HasAttribute<ApplicationLevelAttribute>() && _parent != null
                ? _parent._settings
                : _settings;


            var inner = settings[typeof (T)].As<Task<T>>();
            settings[typeof (T)] = Task.Factory.StartNew(() => {
                alteration(inner.Result);

                return inner.Result;
            });
        }

        public void Replace<T>(T settings) where T : class
        {
            _settings[typeof(T)] = toTask(settings);
        }

        public bool HasExplicit<T>() 
        {
            return _settings.Has(typeof (T));
        }


        public interface IDefaultMaker
        {
            object MakeDefault();
        }

        private static Task<T> toTask<T>(T value)
        {
            var task = new TaskCompletionSource<T>();
            task.SetResult(value);

            return task.Task;
        }

        public class DefaultMaker<T> : IDefaultMaker
        {
            public object MakeDefault()
            {
                return toTask(default(T));
            }
        }

        public class AppSettingMaker<T> : IDefaultMaker where T : class, new()
        {
            public object MakeDefault()
            {
                return Task.Factory.StartNew(() => SettingsProvider.Value.SettingsFor<T>());
            }
        }

        public void Register(ServiceGraph graph)
        {
            _settings.Each((t, o) => {
                var registrar = typeof (Registrar<>).CloseAndBuildAs<IRegistrar>(o, t);
                registrar.Register(graph);
            });
        }

        public interface IRegistrar
        {
            void Register(ServiceGraph graph);
        }

        public class Registrar<T> : IRegistrar
        {
            private readonly Task<T> _task;

            public Registrar(Task<T> task)
            {
                _task = task;
            }

            public void Register(ServiceGraph graph)
            {
                graph.SetServiceIfNone(typeof(T), ObjectDef.ForValue(_task.Result));
            }
        }
    }





}