using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;

using Task = System.Threading.Tasks.Task;

namespace Gaijin.VisualDagor;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid(Constants.PackageGuidString)]
public sealed class Package : AsyncPackage
{
    private readonly StartupProjectWatcher startupProjectWatcher;

    public Package()
    {
        startupProjectWatcher = new StartupProjectWatcher();
    }

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await base.InitializeAsync(cancellationToken, progress);
        await startupProjectWatcher.InitializeAsync(this);
    }

    protected override void Dispose(bool disposing)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        base.Dispose(disposing);
        startupProjectWatcher.Dispose();
    }
}
