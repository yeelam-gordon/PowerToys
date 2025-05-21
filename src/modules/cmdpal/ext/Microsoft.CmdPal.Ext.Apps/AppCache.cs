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
    private bool _win32Initialized = false;
    private bool _uwpInitialized = false;
    private readonly object _initLock = new object();
    private Task _initializationTask; // Track the initialization task

    // Public properties to check initialization state
    public bool IsInitialized => _isInitialized;
    public bool IsWin32Initialized => _win32Initialized;
    public bool IsUWPInitialized => _uwpInitialized;

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
        // Only allow full initialization to happen once
        if (_isInitialized && _win32Initialized && _uwpInitialized)
        {
            return;
        }
        
        lock (_initLock)
        {
            if (_isInitialized && _win32Initialized && _uwpInitialized)
            {
                return;
            }
            
            // We continue with initialization for any components not yet initialized
        }

        bool anySucceeded = false;

        // Initialize Win32 programs if not already initialized
        if (!_win32Initialized)
        {
            try 
            {
                await Task.Run(() => _win32ProgramRepository.IndexPrograms());
                
                lock (_initLock)
                {
                    _win32Initialized = true;
                    anySucceeded = true;
                }
                ManagedCommon.Logger.LogTrace("Win32 programs initialized successfully");
            }
            catch (System.Exception ex)
            {
                // Log error but continue with UWP initialization
                ManagedCommon.Logger.LogError($"Error in Win32 programs initialization: {ex.Message}");
                ManagedCommon.Logger.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        // Initialize UWP apps if not already initialized
        if (!_uwpInitialized)
        {
            try 
            {
                await Task.Run(() => 
                {
                    _packageRepository.IndexPrograms();
                    UpdateUWPIconPath(ThemeHelper.GetCurrentTheme());
                });
                
                lock (_initLock)
                {
                    _uwpInitialized = true;
                    anySucceeded = true;
                }
                ManagedCommon.Logger.LogTrace("UWP applications initialized successfully");
            }
            catch (System.Exception ex)
            {
                // Log error but don't fail completely
                ManagedCommon.Logger.LogError($"Error in UWP applications initialization: {ex.Message}");
                ManagedCommon.Logger.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        // Consider initialization complete if at least one repository initialized successfully
        if (anySucceeded)
        {
            AllAppsSettings.Instance.LastIndexTime = DateTime.Today;
            
            lock (_initLock)
            {
                _isInitialized = true;
            }
        }
        else
        {
            ManagedCommon.Logger.LogError("All AppCache initialization attempts failed");
            throw new System.Exception("Failed to initialize AppCache - all program repositories failed to initialize");
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
