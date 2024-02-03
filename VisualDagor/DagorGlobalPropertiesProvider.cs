using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Build;


namespace Gaijin.VisualDagor
{
    [ExportBuildGlobalPropertiesProvider(true)]
    [AppliesTo(ProjectCapabilities.AlwaysApplicable)]

    public class DagorGlobalPropertiesProvider :
        ProjectValueDataSourceBase<IImmutableDictionary<string, string>>,
        IProjectGlobalPropertiesProvider
    {
#if DEBUG
        static DagorGlobalPropertiesProvider()
        {
            // Note that the type ctor is invoked, but not the ctor, this generally means that the IProjectService
            // version referenced by the package is not using the proper version (has to match vs16 or vs15)
        }
#endif

        /// <summary>
        /// A value that increments with each new map of properties.
        /// </summary>
        private volatile IComparable version = 0L;

        /// <summary>
        /// The block to post to when publishing new values.
        /// </summary>
        private ITargetBlock<IProjectVersionedValue<IImmutableDictionary<string, string>>> targetBlock;

        /// <summary>
        /// The backing field for the <see cref="SourceBlock"/> property.
        /// </summary>
        private IReceivableSourceBlock<IProjectVersionedValue<IImmutableDictionary<string, string>>> publicBlock;

        /// <summary>
        /// Initializes a new instance of the <see cref="DagorGlobalPropertiesProvider"/> class.
        /// </summary>
        /// <param name="commonServices">The CPS common services.</param>
        [ImportingConstructor]
        public DagorGlobalPropertiesProvider(IProjectService service)
            : base(service.Services)
        {
        }

        /// <inheritdoc />
        public override NamedIdentity DataSourceKey { get; } = new NamedIdentity("DagorGlobalProperties");

        /// <inheritdoc />
        public override IComparable DataSourceVersion => version;

        /// <inheritdoc />
        public override IReceivableSourceBlock<IProjectVersionedValue<IImmutableDictionary<string, string>>> SourceBlock
        {
            get
            {
                EnsureInitialized();
                return publicBlock;
            }
        }

        /// <summary>
        /// See <see cref="IProjectGlobalPropertiesProvider"/>
        /// </summary>
        public async Task<IImmutableDictionary<string, string>> GetGlobalPropertiesAsync(CancellationToken cancellationToken)
        {
            var current = Empty.PropertiesMap;

            return current;
        }

        /// <inheritdoc />
        protected override void Initialize()
        {
            base.Initialize();
            var broadcastBlock = new BroadcastBlock<IProjectVersionedValue<IImmutableDictionary<string, string>>>(
                null,
                new DataflowBlockOptions() { NameFormat = "DagorGlobalProperties: {1}" });

            publicBlock = broadcastBlock.SafePublicize();
            targetBlock = broadcastBlock;

            // Hook up some events, or dependencies, that calculate new properties and post to the target block as needed.
            // Posting to the target block with an incremented DataSourceVersion will trigger a new project evaluation with
            // your new properties.
            targetBlock.Post(
                new ProjectVersionedValue<IImmutableDictionary<string, string>>(
                    ImmutableDictionary<string, string>.Empty,
                    ImmutableDictionary<NamedIdentity, IComparable>.Empty.Add(DataSourceKey, DataSourceVersion)));
        }
    }
}