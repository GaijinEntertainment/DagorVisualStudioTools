using System;
using System.Collections.Generic;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.VCProjectEngine;

using Task = System.Threading.Tasks.Task;

namespace Gaijin.VisualDagor;

internal sealed class StartupProjectWatcher : IDisposable, IVsSelectionEvents, IVsSolutionEvents3, IVsUpdateSolutionEvents2
{
    private const int S_OK = VSConstants.S_OK;
    private const uint VSCOOKIE_NIL = VSConstants.VSCOOKIE_NIL;

    private DTE appObject;

    private IVsMonitorSelection selectionMonitor;
    private uint selectionEventsCookie = VSCOOKIE_NIL;

    private IVsSolution2 solutionService;
    private uint solutionEventsCookie = VSCOOKIE_NIL;

    private IVsSolutionBuildManager2 solutionBuildService;
    private uint updateSolutionEventsCookie = VSCOOKIE_NIL;

    private IVsHierarchy startupProject = null;
    private readonly List<IVsHierarchy> dagorSharedProjects = [];

    /// <summary>
    /// Initializes the singleton instance of the watcher.`
    /// </summary>
    /// <param name="package">Owner package, not null.</param>
    public async Task InitializeAsync(AsyncPackage package)
    {
        appObject = await package.GetServiceAsync<SDTE, DTE>();

        selectionMonitor = await package.GetServiceAsync<SVsShellMonitorSelection, IVsMonitorSelection>();
        solutionService = await package.GetServiceAsync<SVsSolution, IVsSolution2>();
        solutionBuildService = await package.GetServiceAsync<SVsSolutionBuildManager, IVsSolutionBuildManager2>();

        // Following code needs to be executed on main thread
        await package.JoinableTaskFactory.SwitchToMainThreadAsync();

        ErrorHandler.ThrowOnFailure(selectionMonitor.AdviseSelectionEvents(this, out selectionEventsCookie));
        ErrorHandler.ThrowOnFailure(solutionService.AdviseSolutionEvents(this, out solutionEventsCookie));
        ErrorHandler.ThrowOnFailure(solutionBuildService.AdviseUpdateSolutionEvents(this, out updateSolutionEventsCookie));

        foreach (var pHierarchy in GetProjects())
        {
            if (pHierarchy.IsCapabilityMatch(Constants.DagorSharedProjectIdentifier))
            {
                dagorSharedProjects.Add(pHierarchy);
            }
        }

        if (ErrorHandler.Succeeded(solutionBuildService.get_StartupProject(out IVsHierarchy hierarchy)))
        {
            OnStartupProjectChanged(hierarchy);
        }
    }

    public void Dispose()
    {
#if DEBUG
        ThreadHelper.ThrowIfNotOnUIThread();
#endif

        if (selectionMonitor is not null && selectionEventsCookie != VSCOOKIE_NIL)
        {
            selectionMonitor.UnadviseSelectionEvents(selectionEventsCookie);
            selectionEventsCookie = VSCOOKIE_NIL;
        }

        if (solutionService is not null && solutionEventsCookie != VSCOOKIE_NIL)
        {
            solutionService.UnadviseSolutionEvents(solutionEventsCookie);
            solutionEventsCookie = VSCOOKIE_NIL;
        }

        if (solutionBuildService is not null && updateSolutionEventsCookie != VSCOOKIE_NIL)
        {
            solutionBuildService.UnadviseUpdateSolutionEvents(updateSolutionEventsCookie);
            updateSolutionEventsCookie = VSCOOKIE_NIL;
        }
    }

    #region IVsSelectionEvents Implementation
    public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (elementid == (uint)VSConstants.VSSELELEMID.SEID_StartupProject && varValueNew is IVsHierarchy hierarchy)
        {
            OnStartupProjectChanged(hierarchy);
        }
        return S_OK;
    }

    public int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew) => S_OK;
    public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive) => S_OK;
    #endregion

    #region IVsSolutionEvents3 Implementation
    public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) => S_OK;
    public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) => S_OK;
    public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
    {
        dagorSharedProjects.Remove(pHierarchy);
        return S_OK;
    }

    public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (pRealHierarchy.IsCapabilityMatch(Constants.DagorSharedProjectIdentifier))
        {
            dagorSharedProjects.Add(pRealHierarchy);
            UpdatedGlobalProperties();
        }
        return S_OK;
    }

    public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) => S_OK;
    public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
    {
        dagorSharedProjects.Remove(pRealHierarchy);
        return S_OK;
    }

    public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution) => S_OK;
    public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) => S_OK;
    public int OnBeforeCloseSolution(object pUnkReserved) => S_OK;
    public int OnAfterCloseSolution(object pUnkReserved) => S_OK;
    public int OnAfterMergeSolution(object pUnkReserved) => S_OK;
    public int OnBeforeOpeningChildren(IVsHierarchy pHierarchy) => S_OK;
    public int OnAfterOpeningChildren(IVsHierarchy pHierarchy) => S_OK;
    public int OnBeforeClosingChildren(IVsHierarchy pHierarchy) => S_OK;
    public int OnAfterClosingChildren(IVsHierarchy pHierarchy) => S_OK;
    #endregion

    #region IVsUpdateSolutionEvents2 Implementation
    public int UpdateSolution_Begin(ref int pfCancelUpdate) => S_OK;
    public int UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand) => S_OK;
    public int UpdateSolution_StartUpdate(ref int pfCancelUpdate) => S_OK;
    public int UpdateSolution_Cancel() => S_OK;
    public int OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (pIVsHierarchy == startupProject)
        {
            UpdatedGlobalProperties();
        }
        return S_OK;
    }

    public int UpdateProjectCfg_Begin(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, ref int pfCancel) => S_OK;
    public int UpdateProjectCfg_Done(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, int fSuccess, int fCancel) => S_OK;
    #endregion

    private void OnStartupProjectChanged(IVsHierarchy pHierarchy)
    {
        var newStartupProject = dagorSharedProjects.Contains(pHierarchy) || !pHierarchy.IsCapabilityMatch(Constants.DagorProjectIdentifier) ? null : pHierarchy;
        if (newStartupProject is null && startupProject is null)
            return;

        startupProject = newStartupProject;
#if DEBUG
        ThreadHelper.ThrowIfNotOnUIThread();
#endif
        UpdatedGlobalProperties();
    }

    private void UpdatedGlobalProperties()
    {
#if DEBUG
        ThreadHelper.ThrowIfNotOnUIThread();
#endif
        try
        {
            string property = startupProject is null ? "" : MSBuildHelper.GetPropertyValueForActiveConfig(startupProject, Constants.PreprocessorProperty);
            for (int i = 0; i < dagorSharedProjects.Count; i++)
            {
                var dagorSharedProject = MSBuildHelper.GetProject(dagorSharedProjects[i])?.Object as VCProject;
                dynamic debugSettings = dagorSharedProject?.ActiveConfiguration?.DebugSettings;
                if (debugSettings is not null)
                {
                    debugSettings.HttpUrl = property;
                }
            }
        }
        catch (Exception) { }
    }

    private IEnumerable<IVsHierarchy> GetProjects(bool includeUnloaded = false)
    {
#if DEBUG
        ThreadHelper.ThrowIfNotOnUIThread();
#endif
        var property = includeUnloaded ? __VSENUMPROJFLAGS.EPF_ALLINSOLUTION : __VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION;

        Guid guid = Guid.Empty;
        solutionService.GetProjectEnum((uint)property, ref guid, out IEnumHierarchies enumerator);

        IVsHierarchy[] hierarchy = [null];
        for (enumerator.Reset(); enumerator.Next(1, hierarchy, out uint fetched) == VSConstants.S_OK && fetched == 1;)
        {
            yield return hierarchy[0];
        }
    }
}
