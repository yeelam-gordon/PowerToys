// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text;

using Common.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SvgPreviewHandlerUnitTests
{
    [STATestClass]
    public class SvgPreviewHandlerHelperTests
    {
        [TestMethod]
        public void CheckBlockedElementsShouldReturnTrueIfABlockedElementIsPresent()
        {
            // Arrange
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<svg width =\"200\" height=\"200\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">");
            svgBuilder.AppendLine("\t<script>alert(\"hello\")</script>");
            svgBuilder.AppendLine("</svg>");
            bool foundFilteredElement;

            // Act
            foundFilteredElement = SvgPreviewHandlerHelper.CheckBlockedElements(svgBuilder.ToString());

            // Assert
            Assert.IsTrue(foundFilteredElement);
        }

        [TestMethod]
        public void CheckBlockedElementsShouldReturnTrueIfBlockedElementsIsPresentInNestedLevel()
        {
            // Arrange
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<svg viewBox=\"0 0 100 100\" xmlns=\"http://www.w3.org/2000/svg\">");
            svgBuilder.AppendLine("\t<circle cx=\"50\" cy=\"50\" r=\"50\">");
            svgBuilder.AppendLine("\t\t<script>alert(\"valid-message\")</script>");
            svgBuilder.AppendLine("\t</circle>");
            svgBuilder.AppendLine("</svg>");
            bool foundFilteredElement;

            // Act
            foundFilteredElement = SvgPreviewHandlerHelper.CheckBlockedElements(svgBuilder.ToString());

            // Assert
            Assert.IsTrue(foundFilteredElement);
        }

        [TestMethod]
        public void CheckBlockedElementsShouldReturnTrueIfMultipleBlockedElementsArePresent()
        {
            // Arrange
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<svg width =\"200\" height=\"200\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">");
            svgBuilder.AppendLine("\t<script>alert(\"valid-message\")</script>");
            svgBuilder.AppendLine("\t<image href=\"valid-url\" height=\"200\" width=\"200\"/>");
            svgBuilder.AppendLine("</svg>");
            bool foundFilteredElement;

            // Act
            foundFilteredElement = SvgPreviewHandlerHelper.CheckBlockedElements(svgBuilder.ToString());

            // Assert
            Assert.IsTrue(foundFilteredElement);
        }

        [TestMethod]
        public void CheckBlockedElementsShouldReturnFalseIfNoBlockedElementsArePresent()
        {
            // Arrange
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<svg viewBox=\"0 0 100 100\" xmlns=\"http://www.w3.org/2000/svg\">");
            svgBuilder.AppendLine("\t<circle cx=\"50\" cy=\"50\" r=\"50\">");
            svgBuilder.AppendLine("\t</circle>");
            svgBuilder.AppendLine("</svg>");
            bool foundFilteredElement;

            // Act
            foundFilteredElement = SvgPreviewHandlerHelper.CheckBlockedElements(svgBuilder.ToString());

            // Assert
            Assert.IsFalse(foundFilteredElement);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("  ")]
        [DataRow(null)]
        public void CheckBlockedElementsShouldReturnFalseIfSvgDataIsNullOrWhiteSpaces(string svgData)
        {
            // Arrange
            bool foundFilteredElement;

            // Act
            foundFilteredElement = SvgPreviewHandlerHelper.CheckBlockedElements(svgData);

            // Assert
            Assert.IsFalse(foundFilteredElement);
        }

        [TestMethod]
        public void BuildCacheKeyShouldReturnSameValueForSameInputs()
        {
            // Arrange
            var firstKey = SvgPreviewCacheHelper.BuildCacheKey(SvgPreviewCacheHelper.CacheVersion, "svg-preview", "sample data");

            // Act
            var secondKey = SvgPreviewCacheHelper.BuildCacheKey(SvgPreviewCacheHelper.CacheVersion, "svg-preview", "sample data");

            // Assert
            Assert.AreEqual(firstKey, secondKey);
        }

        [TestMethod]
        public void BuildCacheKeyShouldReturnDifferentValueForDifferentInputs()
        {
            // Arrange
            var firstKey = SvgPreviewCacheHelper.BuildCacheKey(SvgPreviewCacheHelper.CacheVersion, "svg-preview", "sample data");

            // Act
            var secondKey = SvgPreviewCacheHelper.BuildCacheKey(SvgPreviewCacheHelper.CacheVersion, "svg-preview", "different data");

            // Assert
            Assert.AreNotEqual(firstKey, secondKey);
        }

        [TestMethod]
        public void BuildCacheKeyShouldIncludeInputBoundaries()
        {
            // Arrange
            var firstKey = SvgPreviewCacheHelper.BuildCacheKey(SvgPreviewCacheHelper.CacheVersion, "svg-preview", "a\n", "b");

            // Act
            var secondKey = SvgPreviewCacheHelper.BuildCacheKey(SvgPreviewCacheHelper.CacheVersion, "svg-preview", "a", "\nb");

            // Assert
            Assert.AreNotEqual(firstKey, secondKey);
        }

        [TestMethod]
        public void GetOrCreateCacheFilePathShouldReuseExistingCacheEntry()
        {
            // Arrange
            var cacheFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var cacheKey = SvgPreviewCacheHelper.BuildCacheKey(SvgPreviewCacheHelper.CacheVersion, "svg-preview", "sample data");
            var cacheFilePath = SvgPreviewCacheHelper.GetOrCreateCacheFilePath(cacheFolder, cacheKey, () => "first");

            try
            {
                // Act
                SvgPreviewCacheHelper.GetOrCreateCacheFilePath(cacheFolder, cacheKey, () => "second");

                // Assert
                Assert.AreEqual("first", File.ReadAllText(cacheFilePath));
            }
            finally
            {
                Directory.Delete(cacheFolder, true);
            }
        }

        [TestMethod]
        public void CleanupCacheFolderShouldKeepCacheBounded()
        {
            // Arrange
            var cacheFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var currentCacheFilePath = Path.Combine(cacheFolder, "current.html");

            try
            {
                Directory.CreateDirectory(cacheFolder);
                File.WriteAllText(currentCacheFilePath, "current");

                for (int index = 0; index < 3; index++)
                {
                    var cacheFilePath = Path.Combine(cacheFolder, $"{index}.html");
                    File.WriteAllText(cacheFilePath, index.ToString(CultureInfo.InvariantCulture));
                    File.SetLastWriteTimeUtc(cacheFilePath, DateTime.UtcNow.AddMinutes(-index));
                }

                // Act
                SvgPreviewCacheHelper.CleanupCacheFolder(cacheFolder, currentCacheFilePath, maxCacheFiles: 2);

                // Assert
                Assert.AreEqual(2, Directory.GetFiles(cacheFolder, "*.html").Length);
                Assert.IsTrue(File.Exists(currentCacheFilePath));
            }
            finally
            {
                Directory.Delete(cacheFolder, true);
            }
        }

        [TestMethod]
        public void CheckBlockedElementsShouldNotResolveExternalEntities()
        {
            // Arrange
            var svgBuilder = new StringBuilder();
            svgBuilder.AppendLine("<!DOCTYPE svg [<!ENTITY external SYSTEM \"file:///C:/Windows/win.ini\">]>");
            svgBuilder.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\">");
            svgBuilder.AppendLine("\t<text>&external;</text>");
            svgBuilder.AppendLine("</svg>");

            // Act
            var foundFilteredElement = SvgPreviewHandlerHelper.CheckBlockedElements(svgBuilder.ToString());

            // Assert
            Assert.IsFalse(foundFilteredElement);
        }
    }
}
