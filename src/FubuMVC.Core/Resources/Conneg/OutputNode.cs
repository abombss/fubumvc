using System;
using System.Collections.Generic;
using System.Linq;
using FubuCore;
using FubuCore.Descriptions;
using FubuMVC.Core.Http;
using FubuMVC.Core.Registration.Nodes;
using FubuMVC.Core.Registration.ObjectGraph;
using FubuMVC.Core.Runtime;
using FubuMVC.Core.Runtime.Conditionals;
using FubuMVC.Core.Runtime.Formatters;

namespace FubuMVC.Core.Resources.Conneg
{
    public interface IOutputNode
    {
        /// <summary>
        /// Add an IFormatter strategy for writing with an optional condition
        /// </summary>
        /// <param name="formatter"></param>
        /// <param name="condition"></param>
        void Add(IFormatter formatter, IConditional condition = null);

        /// <summary>
        /// Add a media writer and optional condition by an open type
        /// of IMediaWriter<T> where T is the resource type
        /// </summary>
        /// <param name="mediaWriterType"></param>
        /// <param name="condition"></param>
        void Add(Type mediaWriterType, IConditional condition = null);

        /// <summary>
        /// Explicitly register an IMediaWriter<T> where T is the resource type
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="condition"></param>
        void Add(object writer, IConditional condition = null);

        /// <summary>
        /// All the explicitly configured Media.
        /// </summary>
        /// <returns></returns>
        IEnumerable<IMedia> Media();

        IEnumerable<IMedia<T>> Media<T>();
            
            
            
        /// <summary>
        /// All the possible mimetypes for the explicitly added writers
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> MimeTypes();

        /// <summary>
        /// Is there at least one writer for this mimetype
        /// </summary>
        /// <param name="mimeType"></param>
        /// <returns></returns>
        bool Writes(MimeType mimeType);

        /// <summary>
        /// Is there at least one writer for this mimetype?
        /// </summary>
        /// <param name="mimeType"></param>
        /// <returns></returns>
        bool Writes(string mimeType);

        /// <summary>
        /// Remove all existing writers
        /// </summary>
        void ClearAll();
    }

    public class OutputNode : BehaviorNode, IMayHaveResourceType, DescribesItself, IOutputNode
    {
        private readonly Type _resourceType;
        private readonly IList<IMedia> _media = new List<IMedia>();
        private ConnegSettings _settings;

        private readonly Lazy<IEnumerable<IMedia>> _allMedia; 

        public OutputNode(Type resourceType)
        {
            if (resourceType == typeof (void))
            {
                throw new ArgumentOutOfRangeException("Void is not a valid resource type");
            }

            _resourceType = resourceType;

            _allMedia = new Lazy<IEnumerable<IMedia>>(() => {
                var settings = _settings ?? new ConnegSettings();

                settings.ApplyRules(this);

                return _media;
            });
        }

        public void Add(IFormatter formatter, IConditional condition = null)
        {
            var writer = typeof (FormatterWriter<>).CloseAndBuildAs<object>(formatter, _resourceType);
            addWriter(condition, writer);
        }

        private void addWriter(IConditional condition, object writer)
        {
            var mediaType = typeof(Media<>).MakeGenericType(_resourceType);
            var media = Activator.CreateInstance(mediaType, writer, condition ?? Always.Flyweight).As<IMedia>();

            _media.Add(media);
        }

        public void Add(Type mediaWriterType, IConditional condition = null)
        {
            if (!mediaWriterType.Closes(typeof (IMediaWriter<>)) || !mediaWriterType.IsConcreteWithDefaultCtor())
            {
                throw new ArgumentOutOfRangeException("mediaWriterType", "mediaWriterType must implement IMediaWriter<T> and have a default constructor");
            }

            var writerType = mediaWriterType.MakeGenericType(_resourceType);
            var writer = Activator.CreateInstance(writerType);
            
            addWriter(condition, writer);
        }

        public void Add(object writer, IConditional condition = null)
        {
            var mediaType = typeof (IMedia<>).MakeGenericType(_resourceType);
            if (writer.GetType().CanBeCastTo(mediaType))
            {
                _media.Add(writer.As<IMedia>());
                return;
            }

            var writerType = typeof(IMediaWriter<>).MakeGenericType(_resourceType);
            if (!writerType.IsAssignableFrom(writer.GetType()))
            {
                throw new ArgumentOutOfRangeException("writer", "writer must implement " + writerType.GetFullName());
            }

            addWriter(condition, writer);
        }



        public IEnumerable<IMedia> Media()
        {
            return _allMedia.Value;
        }

        public IEnumerable<IMedia> Explicits
        {
            get
            {
                return _media;
            }
        } 

        public IEnumerable<IMedia<T>> Media<T>()
        {
            return Media().OfType<IMedia<T>>();
        }

        public override BehaviorCategory Category
        {
            get { return BehaviorCategory.Output; }
        }


        public Type ResourceType
        {
            get { return _resourceType; }
        }

        public override string Description
        {
            get { return ToString(); }
        }

        /// <summary>
        /// Use this if you want to override the handling for 
        /// the resource not being found on a chain by chain
        /// basis
        /// </summary>
        public ObjectDef ResourceNotFound { get; set; }

        /// <summary>
        /// Use the specified type T as the resource not found handler strategy
        /// for only this chain
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void UseForResourceNotFound<T>() where T : IResourceNotFoundHandler
        {
            ResourceNotFound = ObjectDef.ForType<T>();
        }

        #region DescribesItself Members

        void DescribesItself.Describe(Description description)
        {
            description.Title = "OutputNode";
            description.ShortDescription = "Render the output for resource " + ResourceType.Name;

            description.AddList("Media", _media);
        }

        #endregion

        #region IMayHaveResourceType Members

        Type IMayHaveResourceType.ResourceType()
        {
            return ResourceType;
        }

        #endregion

        protected override ObjectDef buildObjectDef()
        {
            var def = new ObjectDef(typeof (OutputBehavior<>), _resourceType);



            
            var collectionType = typeof (IMediaCollection<>).MakeGenericType(_resourceType);
            var collection = typeof (MediaCollection<>).CloseAndBuildAs<object>(this, _resourceType);

            def.DependencyByValue(collectionType, collection);

            if (ResourceNotFound != null)
            {
                def.Dependency(typeof(IResourceNotFoundHandler), ResourceNotFound);
            }

            return def;
        }



        public void ClearAll()
        {
            _media.Clear();
        }

        public override string ToString()
        {
            return _media.Select(x => x.ToString()).Join(", ");
        }

        public IEnumerable<string> MimeTypes()
        {
            return _allMedia.Value.SelectMany(x => x.Mimetypes).Distinct();
        }

        public bool Writes(MimeType mimeType)
        {
            return MimeTypes().Contains(mimeType.ToString());
        }

        public bool Writes(string mimeType)
        {
            return MimeTypes().Contains(mimeType);
        }

        public void UseSettings(ConnegSettings settings)
        {
            _settings = settings;
        }
    }
}