// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.UnitTests;

[TestClass]
public sealed class SignoffTransformTests
{
    private static readonly IProgress<double> NoProgress = new Progress<double>();

    private static async Task<string> TransformTextAsync(PasteFormats format, DataPackage input)
    {
        var output = await TransformHelpers.TransformAsync(format, input.GetView(), CancellationToken.None, NoProgress);
        var view = output.GetView();
        return view.Contains(StandardDataFormats.Text) ? await view.GetTextAsync() : string.Empty;
    }

    private static DataPackage TextPackage(string text)
    {
        var pkg = new DataPackage();
        pkg.SetText(text);
        return pkg;
    }

    private static DataPackage TextAndHtmlPackage(string text, string html)
    {
        var pkg = new DataPackage();
        pkg.SetText(text);
        pkg.SetHtmlFormat(html);
        return pkg;
    }

    private static DataPackage HtmlOnlyPackage(string html)
    {
        var pkg = new DataPackage();
        pkg.SetHtmlFormat(html);
        return pkg;
    }

    // ---- P0: paste-as-plain-text strips rich formatting -----------------------
    [TestMethod]
    public async Task PlainText_StripsHtmlFormatting()
    {
        var pkg = TextAndHtmlPackage("Hello World", "<b>Hello</b> <i>World</i>");
        var result = await TransformTextAsync(PasteFormats.PlainText, pkg);

        Assert.AreEqual("Hello World", result, "PlainText must return the plain text payload verbatim.");
        StringAssert.DoesNotMatch(result, new System.Text.RegularExpressions.Regex("<[a-zA-Z/]"), "PlainText output must not contain HTML tags.");
    }

    // ---- P0: paste-as-markdown converts HTML structure ------------------------
    [TestMethod]
    public async Task Markdown_ConvertsHtmlHeadingAndBold()
    {
        var pkg = HtmlOnlyPackage("<h1>Title</h1><p>Hello <b>World</b></p>");
        var result = await TransformTextAsync(PasteFormats.Markdown, pkg);

        StringAssert.Contains(result, "# Title", "Markdown must convert <h1> to an ATX heading.");
        StringAssert.Contains(result, "**World**", "Markdown must convert <b> to bold.");
        Assert.IsFalse(result.Contains("<h1>"), "Markdown output must not contain raw HTML tags.");
    }

    // ---- P0: paste-as-json converts CSV --------------------------------------
    [TestMethod]
    public async Task Json_ConvertsCsv()
    {
        var pkg = TextPackage("a,b,c\r\nd,e,f");
        var result = await TransformTextAsync(PasteFormats.Json, pkg);

        using var doc = JsonDocument.Parse(result);
        Assert.AreEqual(JsonValueKind.Array, doc.RootElement.ValueKind, "CSV must serialize to a JSON array.");
        Assert.AreEqual(2, doc.RootElement.GetArrayLength(), "Two CSV rows expected.");
        Assert.AreEqual("a", doc.RootElement[0][0].GetString());
        Assert.AreEqual("f", doc.RootElement[1][2].GetString());
    }

    // ---- P1: paste-as-json converts XML --------------------------------------
    [TestMethod]
    public async Task Json_ConvertsXml()
    {
        var pkg = TextPackage("<root><item>1</item></root>");
        var result = await TransformTextAsync(PasteFormats.Json, pkg);

        using var doc = JsonDocument.Parse(result);
        Assert.IsTrue(doc.RootElement.TryGetProperty("root", out var root), "XML root element must appear as a JSON property.");
        Assert.AreEqual("1", root.GetProperty("item").GetString());
    }

    // ---- P1: valid JSON is passed through untouched --------------------------
    [TestMethod]
    public async Task Json_PassesThroughExistingJson()
    {
        var pkg = TextPackage("{\"x\":1}");
        var result = await TransformTextAsync(PasteFormats.Json, pkg);

        Assert.AreEqual("{\"x\":1}", result, "Already-JSON input must be returned unchanged.");
    }

    // ---- P1: JsonHelper "never throws" contract; no text -> empty string ------
    [TestMethod]
    public async Task Json_NoTextReturnsEmpty()
    {
        var pkg = HtmlOnlyPackage("<b>no plain text here</b>");
        var result = await TransformTextAsync(PasteFormats.Json, pkg);

        Assert.AreEqual(string.Empty, result, "Json transform must return empty (not throw) when clipboard has no text.");
    }

    // ---- P2: format metadata drives core-vs-AI gating -------------------------
    [TestMethod]
    [DataRow(PasteFormats.PlainText, true, false)]
    [DataRow(PasteFormats.Markdown, true, false)]
    [DataRow(PasteFormats.Json, true, false)]
    [DataRow(PasteFormats.KernelQuery, false, true)]
    [DataRow(PasteFormats.CustomTextTransformation, false, true)]
    public void Metadata_CoreVsAiGating(PasteFormats format, bool expectedCore, bool expectedRequiresAI)
    {
        var attr = typeof(PasteFormats).GetField(format.ToString())
            .GetCustomAttribute<PasteFormatMetadataAttribute>();

        Assert.IsNotNull(attr, $"{format} must carry PasteFormatMetadata.");
        Assert.AreEqual(expectedCore, attr.IsCoreAction, $"{format}.IsCoreAction");
        Assert.AreEqual(expectedRequiresAI, attr.RequiresAIService, $"{format}.RequiresAIService");
    }
}
