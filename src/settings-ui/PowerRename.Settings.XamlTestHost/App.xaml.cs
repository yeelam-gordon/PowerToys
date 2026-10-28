// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace PowerRename.Settings.XamlTestHost;

public sealed partial class ConverterTestApplication : Application
{
    public ConverterTestApplication()
    {
        Environment.ExitCode = 1;
        Console.WriteLine("CONVERTER_UNIT_APP_CONSTRUCTING");
        InitializeComponent();
        Console.WriteLine("CONVERTER_UNIT_APP_CONSTRUCTED");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Console.WriteLine("CONVERTER_UNIT_APP_LAUNCHED");
        DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();
        if (dispatcher == null || !dispatcher.TryEnqueue(RunAssertions))
        {
            Console.Error.WriteLine("CONVERTER_UNIT_DISPATCHER_REFUSED");
            Exit();
        }
    }

    private void RunAssertions()
    {
        try
        {
            Console.WriteLine("CONVERTER_UNIT_BODY_START");
            var fixture = new global::ViewModelTests.PowerRename();
            fixture.SetUpStubSettingUtils();
            fixture.VerifyConfiguredContextMenuModeConversion();
            Console.WriteLine("CONVERTER_UNIT_RESULT=" + JsonSerializer.Serialize(new { Status = "PASS", Cases = 1, Passed = 1 }));
            Environment.ExitCode = 0;
        }
        catch (Exception error)
        {
            Environment.ExitCode = 1;
            Console.Error.WriteLine("CONVERTER_UNIT_ERROR_HRESULT=" + error.HResult);
            Console.Error.WriteLine(error);
        }
        finally
        {
            Console.WriteLine("CONVERTER_UNIT_EXIT_REQUESTED");
            Console.Out.Flush();
            Console.Error.Flush();
            Exit();
        }
    }
}
