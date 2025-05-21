// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Storage;
using Microsoft.CmdPal.Ext.Apps.Utils;

namespace Microsoft.CmdPal.Ext.Apps;

public sealed partial class AppCache : IDisposable
{
    private Win32ProgramFileSystemWatchers _win32ProgramRepositoryHelper;

    private PackageRepository _packageRepository;

    private Win32ProgramRepository _win32ProgramRepository;

    private bool _disposed;

    public IList<Win32Program> Win32s => _win32ProgramRepository.Items;

    public IList<UWPApplication> UWPs => _packageRepository.Items;

    public static readonly Lazy<AppCache> Instance = new(() => new());
    
    private bool _isInitialized = false;
    private readonly object _initLock = new object();
    private Task _initializationTask; // Track the initialization task

    // Public property to check initialization state
    public bool IsInitialized => _isInitialized;

    public AppCache()
    {
        _win32ProgramRepositoryHelper = new Win32ProgramFileSystemWatchers();
        _win32ProgramRepository = new Win32ProgramRepository(_win32ProgramRepositoryHelper.FileSystemWatchers.Cast<IFileSystemWatcherWrapper>().ToList(), AllAppsSettings.Instance, _win32ProgramRepositoryHelper.PathsToWatch);

        _packageRepository = new PackageRepository(new PackageCatalogWrapper());
        
        // Start initialization in background to maintain compatibility with existing code
        // that expects constructor to initialize everything
        _initializationTask = InitializeAsync();
    }
    
    // Wait for initialization to complete without starting a new initialization
    public async Task WaitForInitializationAsync()
    {
        if (_isInitialized)
        {
            return;
        }
        
        if (_initializationTask != null)
        {
            await _initializationTask;
        }
    }
    
    public async Task InitializeAsync()
    {
        // Only allow initialization to happen once
        if (_isInitialized)
        {
            return;
        }
        
        lock (_initLock)
        {
            if (_isInitialized)
            {
                return;
            }
            
            // Set initialization flag before proceeding to prevent duplicate initialization
        }

        try 
        {
            var indexWin32Task = Task.Run(() => _win32ProgramRepository.IndexPrograms());
            var indexPackagesTask = Task.Run(() => 
            {
                _packageRepository.IndexPrograms();
                UpdateUWPIconPath(ThemeHelper.GetCurrentTheme());
            });

            // Use WhenAll instead of WaitAll to properly await both tasks
            await Task.WhenAll(indexWin32Task, indexPackagesTask);

            AllAppsSettings.Instance.LastIndexTime = DateTime.Today;
            
            // Only mark as initialized if we successfully completed initialization
            lock (_initLock)
            {
                _isInitialized = true;
            }
        }
        catch (System.Exception ex)
        {
            // Log error but don't mark as initialized so we can try again
            ManagedCommon.Logger.LogError($"Error in AppCache initialization: {ex.Message}");
            throw; // Re-throw to let caller handle
        }
    }

    private void UpdateUWPIconPath(Theme theme)
    {
        if (_packageRepository != null)
        {
            foreach (UWPApplication app in _packageRepository)
            {
                app.UpdateLogoPath(theme);
            }
        }
    }

    public bool ShouldReload() => _packageRepository.ShouldReload() || _win32ProgramRepository.ShouldReload();

    public void ResetReloadFlag()
    {
        _packageRepository.ResetReloadFlag();
        _win32ProgramRepository.ResetReloadFlag();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _win32ProgramRepositoryHelper?.Dispose();
                _disposed = true;
            }
        }
    }
}
