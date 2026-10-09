// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Globalization;
using FancyZonesCLI.CommandLine;
using Resources = FancyZonesCLI.Properties.Resources;

namespace UnitTestsFancyZonesEditor;

[TestClass]
public sealed class FancyZonesCliSetLayoutTests
{
    [DataTestMethod]
    [DataRow("set-layout grid --monitor abc --help", false)]
    [DataRow("set-layout grid --monitor abc --help", true)]
    [DataRow("set-layout grid --monitor 2147483648 --help", false)]
    [DataRow("set-layout grid --monitor 2147483648 --help", true)]
    [DataRow("set-layout grid --monitor 0 --help", false)]
    [DataRow("set-layout grid --monitor -1 --help", false)]
    [DataRow("set-layout grid --monitor 1 --all --help", false)]
    [DataRow("set-layout grid --monitor 1 --all true --all false --help", false)]
    [DataRow("set-layout grid --monitor 1 --all true --all false --help", true)]
    [DataRow("set-layout grid --monitor 1 --all=invalid --help", false)]
    [DataRow("--help", false)]
    [Timeout(5000)]
    public async Task HelpClearsValidationErrorsWithoutExecutingHandlers(string arguments, bool responseFile)
    {
        int calls = 0;
        var root = CreateRoot(() => calls++);
        string? path = null;
        try
        {
            string[] args = arguments.Split(' ');
            if (responseFile)
            {
                path = Path.Combine(Path.GetTempPath(), $"fancyzones-cli-{Guid.NewGuid():N}.rsp");
                File.WriteAllText(path, arguments);
                args = ["@" + path];
            }

            var parsed = root.Parse(args);
            using var stdout = new StringWriter(CultureInfo.InvariantCulture);
            using var stderr = new StringWriter(CultureInfo.InvariantCulture);
            int exit = await parsed.InvokeAsync(new InvocationConfiguration { Output = stdout, Error = stderr });

            Assert.IsInstanceOfType(parsed.Action, typeof(HelpAction));
            Assert.AreEqual(0, exit);
            Assert.IsFalse(string.IsNullOrWhiteSpace(stdout.ToString()));
            Assert.AreEqual(string.Empty, stderr.ToString());
            Assert.AreEqual(0, calls);
        }
        finally
        {
            if (path is not null)
            {
                File.Delete(path);
            }
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(5000)]
    public async Task MissingMonitorValueDoesNotThrowWhenHelpIsConsumed(bool responseFile)
    {
        int calls = 0;
        string path = Path.Combine(Path.GetTempPath(), $"fancyzones-cli-{Guid.NewGuid():N}.rsp");
        try
        {
            string[] args = ["set-layout", "grid", "--monitor", "--help"];
            if (responseFile)
            {
                File.WriteAllText(path, "set-layout grid --monitor --help");
                args = ["@" + path];
            }

            var parsed = CreateRoot(() => calls++).Parse(args);
            using var stdout = new StringWriter(CultureInfo.InvariantCulture);
            using var stderr = new StringWriter(CultureInfo.InvariantCulture);
            Assert.IsInstanceOfType(parsed.Action, typeof(ParseErrorAction));
            Assert.IsTrue(parsed.Errors.Count > 0);
            Assert.AreNotEqual(0, await parsed.InvokeAsync(new InvocationConfiguration { Output = stdout, Error = stderr }));
            StringAssert.Contains(stderr.ToString(), parsed.Errors[0].Message);
            Assert.AreEqual(0, calls);
        }
        finally
        {
            if (responseFile)
            {
                File.Delete(path);
            }
        }
    }

    [DataTestMethod]
    [DataRow("set-layout grid --monitor abc")]
    [DataRow("set-layout grid --monitor 2147483648")]
    [DataRow("set-layout grid --monitor")]
    [DataRow("set-layout grid --monitor 0")]
    [DataRow("set-layout grid --monitor -1")]
    [DataRow("set-layout grid --monitor 1 --all")]
    [DataRow("set-layout grid --monitor 1 --monitor 2")]
    [DataRow("set-layout grid --monitor 1 --all=true --all=false")]
    [DataRow("set-layout grid --monitor 1 --all=invalid")]
    [Timeout(5000)]
    public async Task InvalidValuesRetainParserDiagnosticsWithoutExecutingHandlers(string arguments)
    {
        int calls = 0;
        var parsed = CreateRoot(() => calls++).Parse(arguments.Split(' '));
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);

        Assert.IsTrue(parsed.Errors.Count > 0);
        int exit = await parsed.InvokeAsync(new InvocationConfiguration { Output = stdout, Error = stderr });

        Assert.AreNotEqual(0, exit);
        StringAssert.Contains(stderr.ToString(), parsed.Errors[0].Message);
        Assert.AreEqual(0, calls);
    }

    [DataTestMethod]
    [DataRow("set-layout grid --monitor 0", true, false)]
    [DataRow("set-layout grid --monitor -1", true, false)]
    [DataRow("set-layout grid --monitor 0 --all", false, true)]
    [DataRow("set-layout grid --monitor 1 --all", false, true)]
    [DataRow("set-layout grid --monitor 1 --all=true", false, true)]
    [DataRow("set-layout grid --monitor 1 --all true", false, true)]
    public void RangeAndMutuallyExclusiveOptionsRemainInvalid(string arguments, bool rangeError, bool conflictError)
    {
        var parsed = CreateRoot(() => Assert.Fail("Parsing must not execute handlers.")).Parse(arguments.Split(' '));

        Assert.AreEqual(rangeError, parsed.Errors.Any(item => item.Message == Resources.set_layout_error_monitor_index));
        Assert.AreEqual(conflictError, parsed.Errors.Any(item => item.Message == Resources.set_layout_error_both_options));
    }

    [DataTestMethod]
    [DataRow("set-layout grid", null, false)]
    [DataRow("set-layout grid --monitor 1", 1, false)]
    [DataRow("set-layout grid --monitor +1", 1, false)]
    [DataRow("set-layout grid --all", null, true)]
    [DataRow("set-layout grid --all=false", null, false)]
    [DataRow("set-layout grid --all false", null, false)]
    [DataRow("set-layout grid --monitor 1 --all=false", 1, false)]
    [DataRow("set-layout grid --monitor 1 --all false", 1, false)]
    [DataRow("set-layout grid --monitor 1 --all --all=false", 1, false)]
    [DataRow("set-layout grid --monitor 1 --all=false --all", 1, false)]
    [DataRow("set-layout grid -m 1 -a:false", 1, false)]
    [Timeout(5000)]
    public async Task ValidOptionsKeepTheirValuesAndDispatchOnce(string arguments, int? monitor, bool all)
    {
        int calls = 0;
        var parsed = CreateRoot(() => calls++).Parse(arguments.Split(' '));
        var options = parsed.CommandResult.Command.Options;

        Assert.AreEqual(0, parsed.Errors.Count);
        Assert.AreEqual(monitor, parsed.GetValue((Option<int?>)options.Single(item => item.Name == "--monitor")));
        Assert.AreEqual(all, parsed.GetValue((Option<bool>)options.Single(item => item.Name == "--all")));
        using var stdout = new StringWriter(CultureInfo.InvariantCulture);
        using var stderr = new StringWriter(CultureInfo.InvariantCulture);
        Assert.AreEqual(0, await parsed.InvokeAsync(new InvocationConfiguration { Output = stdout, Error = stderr }));
        Assert.AreEqual(string.Empty, stderr.ToString());
        Assert.AreEqual(1, calls);
    }

    [DataTestMethod]
    [DataRow("en-US")]
    [DataRow("ar-SA")]
    public void MonitorValidationUsesTheParsersIntegerCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var root = CreateRoot(() => Assert.Fail("Parsing must not execute handlers."));
            string positive = CultureInfo.CurrentCulture.NumberFormat.PositiveSign + "1";
            string negative = CultureInfo.CurrentCulture.NumberFormat.NegativeSign + "1";
            var valid = root.Parse(["set-layout", "grid", "--monitor", positive]);
            var invalid = root.Parse(["set-layout", "grid", "--monitor", negative]);
            var conflict = root.Parse(["set-layout", "grid", "--monitor", positive, "--all"]);

            Assert.AreEqual(0, valid.Errors.Count);
            Assert.AreEqual<int?>(1, valid.GetValue((Option<int?>)valid.CommandResult.Command.Options.Single(item => item.Name == "--monitor")));
            Assert.IsTrue(invalid.Errors.Any(item => item.Message == Resources.set_layout_error_monitor_index));
            Assert.IsTrue(conflict.Errors.Any(item => item.Message == Resources.set_layout_error_both_options));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static RootCommand CreateRoot(Action handler)
    {
        var root = FancyZonesCliCommandFactory.CreateRootCommand();
        ReplaceActions(root, handler);
        return root;
    }

    private static void ReplaceActions(Command command, Action handler)
    {
        command.SetAction(_ => handler());
        foreach (var child in command.Subcommands)
        {
            ReplaceActions(child, handler);
        }
    }
}
