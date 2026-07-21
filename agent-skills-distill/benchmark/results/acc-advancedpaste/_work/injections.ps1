# 10 UI-observable behavioral injections for the AdvancedPaste sign-off.
# Each entry: id, file (relative to PowerToys root), find/repl (exact source),
# and the checklist item expected to catch it.
# find/repl are built from single-quoted line arrays joined with CRLF to avoid
# any quote/newline escaping ambiguity.
$PTRoot = "C:\s\PowerToys"
function J([string[]]$lines) { $lines -join "`r`n" }

$Injections = @(
    @{ id="I1"; check="CHK-01"; file="src\modules\AdvancedPaste\AdvancedPaste\Helpers\TransformHelpers.cs";
       find=(J @('        return CreateDataPackageFromText(await clipboardData.GetTextOrEmptyAsync());'));
       repl=(J @('        return CreateDataPackageFromText((await clipboardData.GetTextOrEmptyAsync()) + "_INJ");'));
       desc="ToPlainTextAsync appends '_INJ' to the plain-text output" }

    @{ id="I2"; check="CHK-02"; file="src\modules\AdvancedPaste\AdvancedPaste\Helpers\MarkdownHelper.cs";
       find=(J @('            return string.IsNullOrEmpty(data) ? string.Empty : ConvertHtmlToMarkdown(CleanHtml(data));'));
       repl=(J @('            return string.IsNullOrEmpty(data) ? string.Empty : data;'));
       desc="ToMarkdownAsync returns raw HTML instead of converting to Markdown" }

    @{ id="I3"; check="CHK-03"; file="src\modules\AdvancedPaste\AdvancedPaste\Helpers\JsonHelper.cs";
       find=(J @('            str = CsvReplaceDoubleQuotationMarksRegex.Replace(str, "\"");', '', '            return str;'));
       repl=(J @('            str = CsvReplaceDoubleQuotationMarksRegex.Replace(str, "\"");', '', '            return "X";'));
       desc="CSV cell processing returns constant 'X' for every cell" }

    @{ id="I4"; check="CHK-04"; file="src\modules\AdvancedPaste\AdvancedPaste\Helpers\JsonHelper.cs";
       find=(J @('                jsonText = JsonConvert.SerializeXmlNode(doc, Newtonsoft.Json.Formatting.Indented);'));
       repl=(J @('                jsonText = string.Empty;'));
       desc="XML-to-JSON branch produces no output (XML conversion disabled)" }

    @{ id="I5"; check="CHK-05"; file="src\modules\AdvancedPaste\AdvancedPaste\Helpers\JsonHelper.cs";
       find=(J @('                _ = JsonDocument.Parse(text);', '                return true;'));
       repl=(J @('                _ = JsonDocument.Parse(text);', '                return false;'));
       desc="IsJson always returns false (valid-JSON passthrough broken)" }

    @{ id="I6"; check="CHK-06"; file="src\modules\AdvancedPaste\AdvancedPaste\Helpers\JsonHelper.cs";
       find=(J @('                    foreach (var line in text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))', '                    {', '                        plainText.Add(line);', '                    }'));
       repl=(J @('                    foreach (var line in text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))', '                    {', '                    }'));
       desc="Plain-text JSON fallback drops all lines (never-throws guard broken)" }

    @{ id="I7"; check="CHK-07"; file="src\modules\AdvancedPaste\AdvancedPaste\ViewModels\OptionsViewModel.cs";
       find=(J @('                if (!IsAllowedByGPO || !_userSettings.IsAIEnabled)', '                {', '                    return false;', '                }'));
       repl=(J @('                if (!IsAllowedByGPO || !_userSettings.IsAIEnabled)', '                {', '                    return true;', '                }'));
       desc="AI gating guard returns true instead of false (AI box enabled when it should be gated)" }

    @{ id="I8"; check="CHK-08"; file="src\modules\AdvancedPaste\AdvancedPaste\Helpers\ClipboardItemHelper.cs";
       find=(J @('                clipboardItem.Content = await clipboardData.GetTextOrEmptyAsync();'));
       repl=(J @('                clipboardItem.Content = "__CORRUPT__";'));
       desc="Clipboard preview Content hard-coded to '__CORRUPT__'" }

    @{ id="I9"; check="CHK-09"; file="src\modules\AdvancedPaste\AdvancedPaste\Models\PasteFormats.cs";
       find=(J @('        IsCoreAction = true,', '        ResourceId = "PasteAsMarkdown",'));
       repl=(J @('        IsCoreAction = false,', '        ResourceId = "PasteAsMarkdown",'));
       desc="Markdown format flagged non-core (drops from the default action list)" }

    @{ id="I10"; check="CHK-10"; file="src\modules\AdvancedPaste\AdvancedPaste\Helpers\MarkdownHelper.cs";
       find=(J @('            string markdown = converter.Convert(html);', '            return markdown;'));
       repl=(J @('            string markdown = converter.Convert(html);', '            return markdown.Replace("*", string.Empty);'));
       desc="Markdown conversion strips all '*' (bold emphasis lost)" }
)
