# Dagor Visual Studio Tools

Tools for building and using [DagorEngine](https://github.com/GaijinEntertainment/DagorEngine) with Visual Studio

## Visual Dagor

A Visual Studio Extension to transform the [prog project](https://github.com/GaijinEntertainment/DagorEngine/blob/main/prog/prog.vcxproj) into a [shared project](https://learn.microsoft.com/en-us/xamarin/cross-platform/app-fundamentals/shared-projects?tabs=windows#what-is-a-shared-project) and set the preprocessor parameters of the common engine according to the Startup Project.
Later other useful Dagor Engine specific features will be added to improve the productivy with Visual Studio

## Gaijin MSBuild Utilities

It is a [library](https://github.com/GaijinEntertainment/DagorEngine/blob/main/prog/_jBuild/msbuild/Gaijin.MSBuild.Utilities.dll) containing custom [MSBuild Taks](https://learn.microsoft.com/en-us/visualstudio/msbuild/task-writing) which are used by the [DagorEngine's Visual Studio Solution](https://github.com/GaijinEntertainment/DagorEngine) to build and provide basic intellisense functionalities.
