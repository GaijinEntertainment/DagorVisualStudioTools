using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Gaijin.VisualDagor;

[Export(ExportContractNames.Scopes.UnconfiguredProject, typeof(IProjectDynamicLoadComponent))]
[AppliesTo(Constants.DagorProjectIdentifier)]
internal class DagorProjectComponent : IProjectDynamicLoadComponent
{
    private bool isInitialized = false;

    public async Task LoadAsync()
    {
        if (isInitialized)
            return;

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (ServiceProvider.GlobalProvider.GetService(typeof(IVsShell)) is not IVsShell shell)
            return;

        if (ErrorHandler.Succeeded(shell.IsPackageLoaded(Constants.PackageGuid, out _)))
            return;

        if (ErrorHandler.Succeeded(shell.LoadPackage(Constants.PackageGuid, out _)))
            isInitialized = true;
    }

    public async Task UnloadAsync() => await Task.CompletedTask;
}
