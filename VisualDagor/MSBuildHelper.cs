using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.VCProjectEngine;

namespace Gaijin.VisualDagor;

internal sealed class MSBuildHelper
{
    public static Project GetProject(IVsHierarchy pHierarchy)
    {
#if DEBUG
        ThreadHelper.ThrowIfNotOnUIThread();
#endif
        pHierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ExtObject, out object extObject);
        return extObject as Project;
    }

    public static string GetPropertyValueForActiveConfig(IVsHierarchy pHierarchy, string propName)
    {
#if DEBUG
        ThreadHelper.ThrowIfNotOnUIThread();
#endif
        var vCProject = GetProject(pHierarchy)?.Object as VCProject;
        if (vCProject?.ActiveConfiguration is VCConfiguration3 vcCfg)
        {
            return vcCfg.GetEvaluatedPropertyValue(propName);
        }

        return "";
    }
}
