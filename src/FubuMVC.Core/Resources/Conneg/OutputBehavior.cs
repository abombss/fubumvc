using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FubuCore;
using FubuCore.Descriptions;
using FubuCore.Logging;
using FubuMVC.Core.Behaviors;
using FubuMVC.Core.Http;
using FubuMVC.Core.Http.Headers;
using FubuMVC.Core.Runtime;
using FubuMVC.Core.Runtime.Conditionals;

namespace FubuMVC.Core.Resources.Conneg
{
    public class OutputBehavior<T> : BasicBehavior where T : class
    {
        private readonly IFubuRequestContext _context;
        private readonly IEnumerable<IMedia<T>> _media;
        private readonly IResourceNotFoundHandler _notFoundHandler;

        public OutputBehavior(IFubuRequestContext context, IEnumerable<IMedia<T>> media, IResourceNotFoundHandler notFoundHandler) : base(PartialBehavior.Executes)
        {
            _context = context;
            _media = media;
            _notFoundHandler = notFoundHandler;
        }

        protected override void afterInsideBehavior()
        {
            Write();
        }

        // SAMPLE: output-behavior-mechanics
        public void Write()
        {
            // If the resource is NOT found, return 
            // invoke the 404 handler
            var resource = _context.Models.Get<T>();
            if (resource == null)
            {
                _context.Writer.WriteResponseCode(HttpStatusCode.NotFound);
                _notFoundHandler.HandleResourceNotFound<T>();

                return;
            }

            // Resolve our CurrentMimeType object from the 
            // HTTP request that we use to represent
            // the mimetypes of the current request
            var mimeTypes = _context.Models.Get<CurrentMimeType>();

            // Select the appropriate media writer
            // based on the mimetype and other runtime
            // conditions
            var media = SelectMedia(mimeTypes, _context);

            if (media == null)
            {
                // If no matching media can be found, write HTTP 406
                _context.Writer.WriteResponseCode(HttpStatusCode.NotAcceptable);
                _context.Writer.Write(MimeType.Text, "406:  Not acceptable");
            }
            else
            {
                // Write the media based on a matching media type
                var outputMimetype = mimeTypes.SelectFirstMatching(media.Mimetypes);
                media.Write(outputMimetype, _context, resource);
            }

            // Write any output headers exposed by the IHaveHeaders
            // interface on the resource type
            WriteHeaders();
        }
        // ENDSAMPLE

        public void WriteHeaders()
        {
            _context.Models.Find<IHaveHeaders>()
                .SelectMany(x => x.Headers)
                .Each(x => x.Write(_context.Writer));
        }

        public virtual IMedia<T> SelectMedia(CurrentMimeType mimeTypes, IFubuRequestContext logger)
        {
            foreach (var acceptType in mimeTypes.AcceptTypes)
            {
                var candidates = _media.Where(x => x.Mimetypes.Contains(acceptType));
                if (candidates.Any())
                {
                    var writer = candidates.FirstOrDefault(x => x.MatchesRequest(_context));
                    if (writer != null)
                    {
                        _context.Logger.DebugMessage(() => new WriterChoice(acceptType, writer, writer.Condition));
                        return writer;
                    }

                    _context.Logger.DebugMessage(() => NoWritersMatch.For(acceptType, candidates));
                }
            }

            if (mimeTypes.AcceptsAny())
            {
                var media = _media.FirstOrDefault(x => x.MatchesRequest(_context));
                _context.Logger.DebugMessage(() => new WriterChoice(MimeType.Any.Value, media, media.Condition));

                return media;
            }

            return null;
        }

        public IEnumerable<IMedia<T>> Media
        {
            get { return _media; }
        }
    }

    public class NoMatchingWriter : LogRecord, DescribesItself
    {
        private readonly CurrentMimeType _mimeType;

        public NoMatchingWriter(CurrentMimeType mimeType)
        {
            _mimeType = mimeType;
        }

        public void Describe(Description description)
        {
            description.Title = "No writers matched the runtime conditions and accept-type: " + _mimeType.AcceptTypes;
        }
    }

    public class WriterChoice : LogRecord, DescribesItself
    {
        private readonly string _mimeType;
        private readonly Description _writer;
        private readonly Description _condition;

        public WriterChoice(string mimeType, object writer, IConditional condition)
        {
            _mimeType = mimeType;
            _writer = Description.For(writer);
            _condition = Description.For(condition);
        }

        public void Describe(Description description)
        {
            description.Title = "Selected writer '{0}'".ToFormat(_writer.Title);

            if (_writer.HasExplicitShortDescription())
            {
                description.Properties["Writer"] = _writer.ShortDescription;
            }
            
            description.Properties["Mimetype"] = _mimeType;
            description.Properties["Condition"] = _condition.Title;
        }
    }

    public class NoWritersMatch : LogRecord, DescribesItself
    {
        public static NoWritersMatch For<T>(string acceptType, IEnumerable<IMedia<T>> candidates)
        {
            var match = new NoWritersMatch{
                WriterList = candidates.Select(writer =>
                {
                    var title = Description.For(writer).Title;
                    var condition = Description.For(writer.Condition).Title;

                    return "{0} ({1})".ToFormat(title, condition);
                }).Join(", ")
            };

            match.MimeType = acceptType;

            return match;
        }

        public string MimeType { get; set; }
        public string WriterList { get; set; }

        public void Describe(Description description)
        {
            description.Title = "Conditions were not met for any available writers for mimetype " + MimeType;
            description.ShortDescription = "Candidate writers:  " + WriterList;
        }
    }
}