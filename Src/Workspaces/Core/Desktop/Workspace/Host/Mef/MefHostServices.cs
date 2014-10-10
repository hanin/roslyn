﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Composition.Convention;
using System.Composition.Hosting;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Host.Mef
{
    public class MefHostServices : HostServices, IMefHostExportProvider
    {
        private readonly CompositionContext compositionContext;

        public MefHostServices(CompositionContext compositionContext)
        {
            this.compositionContext = compositionContext;
        }

        public static MefHostServices Create(CompositionContext compositionContext)
        {
            if (compositionContext == null)
            {
                throw new ArgumentNullException(nameof(compositionContext));
            }

            return new MefHostServices(compositionContext);
        }

        public static MefHostServices Create(IEnumerable<System.Reflection.Assembly> assemblies)
        {
            if (assemblies == null)
            {
                throw new ArgumentNullException("assemblies");
            }

            var compositionConfiguration = new ContainerConfiguration().WithAssemblies(assemblies);
            var container = compositionConfiguration.CreateContainer();
            return new MefHostServices(container);
        }

        protected internal override HostWorkspaceServices CreateWorkspaceServices(Workspace workspace)
        {
            return new MefWorkspaceServices(this, workspace);
        }

        IEnumerable<Lazy<TExtension>> IMefHostExportProvider.GetExports<TExtension>()
        {
            return compositionContext.GetExports<TExtension>().Select(e => new Lazy<TExtension>(() => e));
        }

        IEnumerable<Lazy<TExtension, TMetadata>> IMefHostExportProvider.GetExports<TExtension, TMetadata>()
        {
            var importer = new WithMetadataImporter<TExtension, TMetadata>();
            compositionContext.SatisfyImports(importer);
            return importer.Exports;
        }

        private class WithMetadataImporter<TExtension, TMetadata>
        {
            [ImportMany]
            public IEnumerable<Lazy<TExtension, TMetadata>> Exports { get; set; }
        }

        #region Defaults

        private static MefHostServices defaultHost;
        public static MefHostServices DefaultHost
        {
            get
            {
                if (defaultHost == null)
                {
                    var host = MefHostServices.Create(MefHostServices.DefaultAssemblies);
                    Interlocked.CompareExchange(ref defaultHost, host, null);
                }

                return defaultHost;
            }
        }

        private static ImmutableArray<Assembly> defaultAssemblies;
        public static ImmutableArray<Assembly> DefaultAssemblies
        {
            get
            {
                if (defaultAssemblies.IsDefault)
                {
                    ImmutableInterlocked.InterlockedInitialize(ref defaultAssemblies, LoadDefaultAssemblies());
                }

                return defaultAssemblies;
            }
        }

        private static ImmutableArray<Assembly> LoadDefaultAssemblies()
        {
            // build a MEF composition using the main workspaces assemblies and the known VisualBasic/CSharp workspace assemblies.
            var assemblyNames = new string[]
            {
                "Microsoft.CodeAnalysis.Workspaces",
                "Microsoft.CodeAnalysis.Workspaces.Desktop",
                "Microsoft.CodeAnalysis.CSharp.Workspaces",
                "Microsoft.CodeAnalysis.CSharp.Workspaces.Desktop",
                "Microsoft.CodeAnalysis.VisualBasic.Workspaces",
                "Microsoft.CodeAnalysis.VisualBasic.Workspaces.Desktop",
            };

            var assemblies = new List<Assembly>();

            var thisAssemblyName = typeof(MefHostServices).Assembly.GetName();
            var assemblyShortName = thisAssemblyName.Name;
            var assemblyVersion = thisAssemblyName.Version;
            var publicKeyToken = thisAssemblyName.GetPublicKeyToken().Aggregate(string.Empty, (s, b) => s + b.ToString("x2"));

            foreach (var assemblyName in assemblyNames)
            {
                LoadAssembly(assemblies,
                    string.Format("{0}, Version={1}, Culture=neutral, PublicKeyToken={2}", assemblyName, assemblyVersion, publicKeyToken));
            }

            return assemblies.ToImmutableArray();
        }

        private static void LoadAssembly(List<Assembly> assemblies, string assemblyName)
        {
            try
            {
                var loadedAssembly = Assembly.Load(assemblyName);
                assemblies.Add(loadedAssembly);
            }
            catch (Exception)
            {
            }
        }

        #endregion
    }
}