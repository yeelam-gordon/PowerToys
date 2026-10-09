// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.Common.CommandLine;

namespace Settings.UI.UnitTests.Cmd;

[TestClass]
public sealed class VersionOptionErrorActionTests
{
    [DataTestMethod]
    [DataRow("--version", false)]
    [DataRow("--version", true)]
    [DataRow("--version --unknown", false)]
    [DataRow("--version --unknown", true)]
    public async Task StandaloneVersionPreservesTheBuiltInOutput(string arguments, bool asynchronous)
    {
        int calls = 0;
        var root = CreateRoot(() => calls++);
        var version = root.Options.OfType<VersionOption>().Single();
        var originalAction = version.Action!;
        using var expected = new StringWriter(CultureInfo.InvariantCulture);
        using var expectedError = new StringWriter(CultureInfo.InvariantCulture);
        Assert.AreEqual(0, await Invoke(root.Parse(arguments.Split(' ')), asynchronous, expected, expectedError));

        VersionOptionErrorAction.Apply(root);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        Assert.AreEqual(0, await Invoke(root.Parse(arguments.Split(' ')), asynchronous, output, error));

        Assert.IsTrue(version.Action!.ClearsParseErrors);
        Assert.AreEqual(originalAction.Terminating, version.Action.Terminating);
        Assert.IsFalse(string.IsNullOrWhiteSpace(expected.ToString()));
        Assert.AreEqual(expected.ToString(), output.ToString());
        Assert.AreEqual(string.Empty, error.ToString());
        Assert.AreEqual(0, calls);
    }

    [DataTestMethod]
    [DataRow("--version list", false)]
    [DataRow("--version list", true)]
    [DataRow("--version --registered", false)]
    [DataRow("--version --registered", true)]
    [DataRow("--version --registered=false", false)]
    [DataRow("--version --registered=false", true)]
    [DataRow("--help --version", false)]
    [DataRow("--help --version", true)]
    public async Task VersionOwnedErrorsReportDiagnosticsWithoutVersionOrHandler(string arguments, bool asynchronous)
    {
        int calls = 0;
        var root = CreateRoot(() => calls++);
        using var versionOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var versionError = new StringWriter(CultureInfo.InvariantCulture);
        Assert.AreEqual(0, await Invoke(root.Parse(["--version"]), asynchronous, versionOutput, versionError));
        VersionOptionErrorAction.Apply(root);
        var version = root.Options.OfType<VersionOption>().Single();
        var parsed = root.Parse(arguments.Split(' '));
        var diagnostic = parsed.Errors.Single(item => item.SymbolResult is OptionResult result && result.Option == version).Message;
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        Assert.AreSame(version.Action, parsed.Action);
        Assert.AreEqual(1, await Invoke(parsed, asynchronous, output, error));
        StringAssert.Contains(error.ToString(), diagnostic);
        Assert.IsFalse(output.ToString().Contains(versionOutput.ToString().Trim(), StringComparison.Ordinal));
        Assert.AreEqual(0, calls);
    }

    [DataTestMethod]
    [DataRow("--version --help", false)]
    [DataRow("--version --help", true)]
    [DataRow("list --help --version", false)]
    [DataRow("list --help --version", true)]
    [DataRow("list --version --help", false)]
    [DataRow("list --version --help", true)]
    [DataRow("list --help", false)]
    [DataRow("list --help", true)]
    public async Task SelectedHelpStillClearsMissingRequiredArguments(string arguments, bool asynchronous)
    {
        int calls = 0;
        var root = CreateRoot(() => calls++);
        VersionOptionErrorAction.Apply(root);
        var parsed = root.Parse(arguments.Split(' '));
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        Assert.IsInstanceOfType(parsed.Action, typeof(HelpAction));
        Assert.AreEqual(0, await Invoke(parsed, asynchronous, output, error));
        Assert.IsFalse(string.IsNullOrWhiteSpace(output.ToString()));
        Assert.AreEqual(string.Empty, error.ToString());
        Assert.AreEqual(0, calls);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task OrdinaryParseErrorsStillDoNotExecuteHandlers(bool asynchronous)
    {
        int calls = 0;
        var root = CreateRoot(() => calls++);
        VersionOptionErrorAction.Apply(root);
        var parsed = root.Parse(["list"]);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        Assert.IsInstanceOfType(parsed.Action, typeof(ParseErrorAction));
        Assert.AreEqual(1, await Invoke(parsed, asynchronous, output, error));
        Assert.IsFalse(string.IsNullOrWhiteSpace(error.ToString()));
        Assert.AreEqual(0, calls);
    }

    private static RootCommand CreateRoot(Action handler)
    {
        var root = new RootCommand("Harmless version-action test");
        root.Options.Add(new Option<bool>("--registered"));
        root.Options.Add(new Option<string>("--required") { Required = true });
        root.SetAction(_ => handler());
        var command = new Command("list");
        command.Arguments.Add(new Argument<string>("value"));
        command.SetAction(_ => handler());
        root.Subcommands.Add(command);
        return root;
    }

    private static Task<int> Invoke(ParseResult parsed, bool asynchronous, TextWriter output, TextWriter error)
    {
        var configuration = new InvocationConfiguration { Output = output, Error = error };
        return asynchronous ? parsed.InvokeAsync(configuration) : Task.FromResult(parsed.Invoke(configuration));
    }
}
