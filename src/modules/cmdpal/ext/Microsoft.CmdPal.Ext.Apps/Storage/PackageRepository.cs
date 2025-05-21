// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Windows.ApplicationModel;

namespace Microsoft.CmdPal.Ext.Apps.Storage;

/// <summary>
/// A repository for storing packaged applications such as UWP apps or appx packaged desktop apps.
/// This repository will also monitor for changes to the PackageCatalog and update the repository accordingly
/// </summary>
internal sealed partial class PackageRepository : ListRepository<UWPApplication>, IProgramRepository
{
    private readonly IPackageCatalog _packageCatalog;

    private bool _isDirty;

    public bool ShouldReload()
    {
        return _isDirty;
    }

    public void ResetReloadFlag()
    {
        _isDirty = false;
    }

    // private readonly PluginInitContext _context;
    public PackageRepository(IPackageCatalog packageCatalog)
    {
        _packageCatalog = packageCatalog ?? throw new ArgumentNullException(nameof(packageCatalog), "PackageRepository expects an interface to be able to subscribe to package events");

        _packageCatalog.PackageInstalling += OnPackageInstalling;
        _packageCatalog.PackageUninstalling += OnPackageUninstalling;
        _packageCatalog.PackageUpdating += OnPackageUpdating;
    }

    public void OnPackageInstalling(PackageCatalog p, PackageInstallingEventArgs args)
    {
        if (args.IsComplete)
        {
            AddPackage(args.Package);
        }
    }

    public void OnPackageUninstalling(PackageCatalog p, PackageUninstallingEventArgs args)
    {
        if (args.Progress == 0)
        {
            RemovePackage(args.Package);
        }
    }

    public void OnPackageUpdating(PackageCatalog p, PackageUpdatingEventArgs args)
    {
        if (args.Progress == 0)
        {
            RemovePackage(args.SourcePackage);
        }

        if (args.IsComplete)
        {
            AddPackage(args.TargetPackage);
        }
    }

    private void AddPackage(Package package)
    {
        var packageWrapper = PackageWrapper.GetWrapperFromPackage(package);
        if (string.IsNullOrEmpty(packageWrapper.InstalledLocation))
        {
            return;
        }

        try
        {
            var uwp = new UWP(packageWrapper);
            uwp.InitializeAppInfo(packageWrapper.InstalledLocation);
            foreach (var app in uwp.Apps)
            {
                app.UpdateLogoPath(ThemeHelper.GetCurrentTheme());
                Add(app);
                _isDirty = true;
            }
        }

        // InitializeAppInfo will throw if there is no AppxManifest.xml for the package.
        // Note there are sometimes multiple packages per product and this doesn't necessarily mean that we haven't found the app.
        // eg. "Could not find file 'C:\\Program Files\\WindowsApps\\Microsoft.WindowsTerminalPreview_2020.616.45.0_neutral_~_8wekyb3d8bbwe\\AppxManifest.xml'."
        catch (System.IO.FileNotFoundException ex)
        {
            Logger.LogError(ex.Message);
        }
    }

    private void RemovePackage(Package package)
    {
        // find apps associated with this package.
        var packageWrapper = PackageWrapper.GetWrapperFromPackage(package);
        var uwp = new UWP(packageWrapper);
        var apps = Items.Where(a => a.Package.Equals(uwp)).ToArray();

        foreach (var app in apps)
        {
            Remove(app);
            _isDirty = true;
        }
    }

    public void IndexPrograms()
    {
        try
        {
            var windows10 = new Version(10, 0);
            var support = Environment.OSVersion.Version.Major >= windows10.Major;

            if (!support)
            {
                ManagedCommon.Logger.LogTrace("PackageRepository: UWP apps not supported on this OS version");
                SetList(Array.Empty<UWPApplication>());
                return;
            }

            try
            {
                // Get all UWP applications from system packages
                var applications = new List<UWPApplication>();
                bool anyPackageProcessed = false;

                // Process packages for current user
                try
                {
                    var packages = CurrentUserPackages().ToList();
                    ManagedCommon.Logger.LogTrace($"PackageRepository: Found {packages.Count} packages for current user");
                    
                    // Process each package individually to allow partial success
                    foreach (var package in packages)
                    {
                        try
                        {
                            var uwp = new UWP(package);
                            uwp.InitializeAppInfo(package.InstalledLocation);
                            
                            // Filter out disabled apps
                            var validApps = uwp.Apps
                                .Where(app => AllAppsSettings.Instance.DisabledProgramSources
                                    .All(x => x.UniqueIdentifier != app.UniqueIdentifier))
                                .ToList();
                            
                            applications.AddRange(validApps);
                            anyPackageProcessed = true;
                            
                            ManagedCommon.Logger.LogTrace($"PackageRepository: Successfully processed package {uwp.FamilyName} with {validApps.Count} apps");
                        }
                        catch (Exception ex)
                        {
                            // Log error for this package but continue with others
                            ManagedCommon.Logger.LogError($"PackageRepository: Error processing package {package.FamilyName}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ManagedCommon.Logger.LogError($"PackageRepository: Error getting packages: {ex.Message}");
                    ManagedCommon.Logger.LogError($"Stack trace: {ex.StackTrace}");
                }
                
                // Set the list with whatever applications we managed to collect
                if (anyPackageProcessed)
                {
                    SetList(applications);
                    ManagedCommon.Logger.LogTrace($"PackageRepository: Indexed {applications.Count} UWP applications");
                }
                else
                {
                    // If no package was processed successfully, log the error but keep any existing items
                    ManagedCommon.Logger.LogError("PackageRepository: Failed to process any UWP packages");
                    if (Items.Count > 0)
                    {
                        ManagedCommon.Logger.LogTrace($"PackageRepository: Keeping {Items.Count} existing UWP applications");
                    }
                }
            }
            catch (Exception ex)
            {
                ManagedCommon.Logger.LogError($"PackageRepository: Error in UWP initialization: {ex.Message}");
                ManagedCommon.Logger.LogError($"Stack trace: {ex.StackTrace}");
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't throw it further
            ManagedCommon.Logger.LogError($"PackageRepository: Critical error in IndexPrograms: {ex.Message}");
            ManagedCommon.Logger.LogError($"Stack trace: {ex.StackTrace}");
        }
    }
}
