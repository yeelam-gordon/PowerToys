// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace Microsoft.CmdPal.UI.ViewModels.Helpers
{
    internal static class LanguageHelpers
    {
        /// <summary>
        /// Checks if the string contains Chinese characters.
        /// </summary>
        /// <param name="text">Text to check</param>
        /// <returns>True if the text contains Chinese characters</returns>
        public static bool ContainsChineseCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            // Unicode ranges for Chinese characters
            // CJK Unified Ideographs: 4E00-9FFF
            // CJK Unified Ideographs Extension A: 3400-4DBF
            // CJK Unified Ideographs Extension B: 20000-2A6DF
            // CJK Unified Ideographs Extension C: 2A700-2B73F
            // CJK Unified Ideographs Extension D: 2B740-2B81F
            // CJK Unified Ideographs Extension E: 2B820-2CEAF
            // CJK Unified Ideographs Extension F: 2CEB0-2EBEF
            // CJK Compatibility Ideographs: F900-FAFF
            return text.Any(c => 
                (c >= 0x4E00 && c <= 0x9FFF) ||
                (c >= 0x3400 && c <= 0x4DBF) ||
                (c >= 0x20000 && c <= 0x2A6DF) ||
                (c >= 0x2A700 && c <= 0x2B73F) ||
                (c >= 0x2B740 && c <= 0x2B81F) ||
                (c >= 0x2B820 && c <= 0x2CEAF) ||
                (c >= 0x2CEB0 && c <= 0x2EBEF) ||
                (c >= 0xF900 && c <= 0xFAFF));
        }

        /// <summary>
        /// Checks if the string contains Japanese characters.
        /// </summary>
        /// <param name="text">Text to check</param>
        /// <returns>True if the text contains Japanese characters</returns>
        public static bool ContainsJapaneseCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            // Unicode ranges for Japanese-specific characters
            // Hiragana: 3040-309F
            // Katakana: 30A0-30FF
            // Katakana Phonetic Extensions: 31F0-31FF
            // Note: Kanji (Chinese characters used in Japanese) are covered by the same ranges
            // as Chinese characters, so we also need to check those ranges
            return text.Any(c =>
                (c >= 0x3040 && c <= 0x309F) ||
                (c >= 0x30A0 && c <= 0x30FF) ||
                (c >= 0x31F0 && c <= 0x31FF) ||
                // Also include Kanji ranges, which overlap with Chinese character ranges
                (c >= 0x4E00 && c <= 0x9FFF));
        }

        /// <summary>
        /// Checks if the string contains characters from languages that don't use spaces between words.
        /// </summary>
        /// <param name="text">Text to check</param>
        /// <returns>True if the text contains characters from non-space-joining languages</returns>
        public static bool ContainsNonSpaceJoiningLanguageCharacters(string text)
        {
            return ContainsChineseCharacters(text) || ContainsJapaneseCharacters(text);
        }
    }
}