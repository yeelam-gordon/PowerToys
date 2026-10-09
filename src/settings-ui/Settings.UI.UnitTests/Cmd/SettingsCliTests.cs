// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.Globalization;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.Settings.Cli;
using PowerToys.Settings.Cli.Helpers;

namespace Settings.UI.UnitTests.Cmd;

[TestClass]
public class SettingsCliTests
{
    private SettingsUtils settingsUtils;
    private MockFileSystem mockFileSystem;

    [TestInitialize]
    public void Setup()
    {
        mockFileSystem = new MockFileSystem();
        settingsUtils = new SettingsUtils(mockFileSystem);
    }

    [TestMethod]
    public void TestGetModulesAndStatus()
    {
        var modules = SettingsCliHelper.GetModulesAndStatus(settingsUtils, _ => null);

        Assert.IsNotNull(modules);
        Assert.IsTrue(modules.Count > 0);
        Assert.IsTrue(modules.ContainsKey("FancyZones"));
        Assert.IsTrue(modules.ContainsKey("AlwaysOnTop"));
        Assert.IsFalse(settingsUtils.SettingsExists());
    }

    [TestMethod]
    public void TestReadOnlyModuleStatusDoesNotOverwriteInvalidSettings()
    {
        const string corruptSettings = "{";
        var settingsFilePath = settingsUtils.GetSettingsFilePath();
        mockFileSystem.AddFile(settingsFilePath, new MockFileData(corruptSettings));

        Assert.ThrowsException<InvalidOperationException>(() =>
            SettingsCliHelper.GetModulesAndStatus(settingsUtils, _ => null));

        Assert.AreEqual(corruptSettings, mockFileSystem.File.ReadAllText(settingsFilePath));
    }

    [TestMethod]
    public void TestGetModuleStatus()
    {
        var status = SettingsCliHelper.GetModuleStatus("fancyzones", settingsUtils, _ => null);

        Assert.AreEqual("FancyZones", status.ModuleName);
        Assert.IsNull(status.GroupPolicy);
    }

    [TestMethod]
    public void TestSetModuleEnabled()
    {
        var disabledState = SettingsCliHelper.SetModuleEnabled("FancyZones", enabled: false, settingsUtils, _ => null, () => EmptyDisposable.Instance);
        Assert.IsFalse(disabledState.Enabled);

        var modulesAfterDisable = SettingsCliHelper.GetModulesAndStatus(settingsUtils, _ => null);
        Assert.IsFalse(modulesAfterDisable["FancyZones"]);
        Assert.IsTrue(settingsUtils.GetSettings<GeneralSettings>().ShowWhatsNewAfterUpdates);

        var enabledState = SettingsCliHelper.SetModuleEnabled("FancyZones", enabled: true, settingsUtils, _ => null, () => EmptyDisposable.Instance);
        Assert.IsTrue(enabledState.Enabled);
    }

    [TestMethod]
    public void TestSetModuleEnabledCreatesSettingsFolderBeforeAcquiringLock()
    {
        var settingsFolderExistsWhenLockAcquired = false;

        SettingsCliHelper.SetModuleEnabled(
            "FancyZones",
            enabled: false,
            settingsUtils,
            _ => null,
            () =>
            {
                settingsFolderExistsWhenLockAcquired = mockFileSystem.Directory.Exists(
                    Path.GetDirectoryName(settingsUtils.GetSettingsFilePath()));
                return EmptyDisposable.Instance;
            });

        Assert.IsTrue(settingsFolderExistsWhenLockAcquired);
    }

    [TestMethod]
    public void TestGroupPolicyOverridesEffectiveState()
    {
        var status = SettingsCliHelper.GetModuleStatus(
            "FancyZones",
            settingsUtils,
            _ => false);

        Assert.IsFalse(status.Enabled);
        Assert.AreEqual("Disabled", status.GroupPolicy);
    }

    [TestMethod]
    public void TestSetModuleEnabledRejectsGroupPolicyLockedModule()
    {
        Assert.ThrowsException<InvalidOperationException>(() =>
            SettingsCliHelper.SetModuleEnabled(
                "FancyZones",
                enabled: true,
                settingsUtils,
                _ => false,
                () => EmptyDisposable.Instance));
        Assert.ThrowsException<InvalidOperationException>(() =>
            SettingsCliHelper.SetModuleEnabled(
                "FancyZones",
                enabled: false,
                settingsUtils,
                _ => true,
                () => EmptyDisposable.Instance));
    }

    [TestMethod]
    public void TestSetModuleEnabledPropagatesSaveFailure()
    {
        var failingSettingsUtils = new FailingSaveSettingsUtils();

        Assert.ThrowsException<IOException>(() =>
            SettingsCliHelper.SetModuleEnabled(
                "FancyZones",
                enabled: false,
                failingSettingsUtils,
                _ => null,
                () => EmptyDisposable.Instance));
    }

    [TestMethod]
    public void TestSetModuleEnabledRejectsWhenSettingsAreLocked()
    {
        Assert.ThrowsException<IOException>(() =>
            SettingsCliHelper.SetModuleEnabled(
                "FancyZones",
                enabled: false,
                settingsUtils,
                _ => null,
                () => throw new IOException("Settings lock is held.")));
        Assert.IsFalse(settingsUtils.SettingsExists());
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public static EmptyDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    [DataTestMethod]
    [DataRow("enable")]
    [DataRow("disable")]
    [DataRow("status")]
    public void TestCommandParsingReportsMissingArguments(string command)
    {
        var parseResult = Program.CreateRootCommand().Parse([command]);

        Assert.IsTrue(parseResult.Errors.Count > 0);
    }

    [DataTestMethod]
    [DataRow("enable")]
    [DataRow("disable")]
    [DataRow("status")]
    public async Task TestMissingModuleArgumentsReturnFailureWithoutExecutingCommands(string command)
    {
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);
        var parseResult = Program.CreateRootCommand().Parse([command]);

        var exitCode = await parseResult.InvokeAsync(new InvocationConfiguration { Output = stdout, Error = stderr });

        Assert.AreNotEqual(0, exitCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(stderr.ToString()));
        Assert.IsFalse(settingsUtils.SettingsExists());
    }

    [DataTestMethod]
    [DataRow("enable")]
    [DataRow("disable")]
    [DataRow("status")]
    public void TestCommandParsingPreservesModuleArgument(string command)
    {
        var root = Program.CreateRootCommand();
        var selectedCommand = root.Subcommands.Single(item => item.Name == command);
        var moduleArg = (Argument<string>)selectedCommand.Arguments.Single();

        var parseResult = root.Parse([command, "FancyZones"]);

        Assert.AreEqual(0, parseResult.Errors.Count);
        Assert.AreSame(selectedCommand, parseResult.CommandResult.Command);
        Assert.AreEqual("FancyZones", parseResult.GetRequiredValue(moduleArg));
        Assert.IsFalse(settingsUtils.SettingsExists());
    }

    [DataTestMethod]
    [DataRow("list", true)]
    [DataRow("list", false)]
    [DataRow("status", true)]
    [DataRow("status", false)]
    public void TestCommandParsingPreservesJsonOption(string command, bool json)
    {
        var root = Program.CreateRootCommand();
        var selectedCommand = root.Subcommands.Single(item => item.Name == command);
        var jsonOpt = (Option<bool>)selectedCommand.Options.Single(item => item.Name == "--json");
        string[] args = command == "status"
            ? [command, "FancyZones", $"--json={json.ToString().ToLowerInvariant()}"]
            : [command, $"--json={json.ToString().ToLowerInvariant()}"];

        var parseResult = root.Parse(args);

        Assert.AreEqual(0, parseResult.Errors.Count);
        Assert.AreEqual(json, parseResult.GetValue(jsonOpt));
        Assert.IsFalse(settingsUtils.SettingsExists());
    }

    [DataTestMethod]
    [DataRow("--help")]
    [DataRow("-h")]
    [DataRow("-?")]
    public async Task TestHelpAliasesDoNotExecuteModuleCommands(string alias)
    {
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);
        var parseResult = Program.CreateRootCommand().Parse(["enable", alias]);

        var exitCode = await parseResult.InvokeAsync(new InvocationConfiguration { Output = stdout, Error = stderr });

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(stdout.ToString(), "module");
        Assert.AreEqual(string.Empty, stderr.ToString());
        Assert.IsFalse(settingsUtils.SettingsExists());
    }

    [DataTestMethod]
    [DataRow(new string[] { "list" }, "list")]
    [DataRow(new string[] { "status", "FancyZones" }, "status")]
    [DataRow(new string[] { "enable", "FancyZones" }, "enable")]
    [DataRow(new string[] { "disable", "FancyZones" }, "disable")]
    [DataRow(new string[] { "--help" }, "help")]
    [DataRow(new string[] { "unexpected", "sensitive-value" }, "unknown")]
    [DataRow(new string[] { }, "none")]
    public void TestTelemetryCommandNameDoesNotIncludeArguments(string[] args, string expected)
    {
        Assert.AreEqual(expected, Program.GetTelemetryCommandName(args));
    }

    private sealed class FailingSaveSettingsUtils : SettingsUtils
    {
        public FailingSaveSettingsUtils()
            : base(new MockFileSystem())
        {
        }

        public override void SaveSettingsOrThrow(string jsonSettings, string powertoy = "", string fileName = SettingsUtils.DefaultFileName)
        {
            throw new IOException("Simulated settings write failure.");
        }
    }
}
