// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Peek.FilePreviewer.Helpers
{
    /// <summary>
    /// Helper class for OCR text extraction from images
    /// </summary>
    internal static class OcrHelper
    {
        /// <summary>
        /// Extract text from a specific point in an image
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="clickPoint">Point where user clicked</param>
        /// <param name="imageControlSize">Size of the image control displaying the image</param>
        /// <param name="actualImageSize">Actual size of the image</param>
        /// <returns>Extracted text at the clicked position, or empty string if no text found</returns>
        public static async Task<string> ExtractTextAtPointAsync(string imagePath, Windows.Foundation.Point clickPoint, 
            Windows.Foundation.Size imageControlSize, Windows.Foundation.Size actualImageSize)
        {
            try
            {
                // Get the OCR language (fallback to English)
                var language = GetOcrLanguage() ?? new Language("en-US");
                var ocrEngine = OcrEngine.TryCreateFromLanguage(language);
                
                if (ocrEngine == null)
                {
                    return string.Empty;
                }

                // Load and process the image
                using var fileStream = File.OpenRead(imagePath);
                var decoder = await BitmapDecoder.CreateAsync(fileStream.AsRandomAccessStream());
                var softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                
                // Calculate scaling factors to map from control coordinates to image coordinates
                var scaleX = actualImageSize.Width / imageControlSize.Width;
                var scaleY = actualImageSize.Height / imageControlSize.Height;
                
                // Scale the click point to image coordinates
                var scaledPoint = new Windows.Foundation.Point(
                    clickPoint.X * scaleX,
                    clickPoint.Y * scaleY);
                
                // Perform OCR
                var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap);
                
                // Find text at the clicked point
                foreach (var line in ocrResult.Lines)
                {
                    foreach (var word in line.Words)
                    {
                        if (word.BoundingRect.Contains(scaledPoint))
                        {
                            return word.Text;
                        }
                    }
                }
                
                return string.Empty;
            }
            catch
            {
                // Return empty string on any error
                return string.Empty;
            }
        }

        /// <summary>
        /// Get the best available OCR language
        /// </summary>
        private static Language? GetOcrLanguage()
        {
            try
            {
                var userLanguages = Windows.System.UserProfile.GlobalizationPreferences.Languages;
                
                foreach (var userLanguage in userLanguages)
                {
                    var language = new Language(userLanguage);
                    if (OcrEngine.IsLanguageSupported(language))
                    {
                        return language;
                    }
                }
                
                // Fallback to English if available
                var englishLanguage = new Language("en-US");
                if (OcrEngine.IsLanguageSupported(englishLanguage))
                {
                    return englishLanguage;
                }
                
                // Return the first available language
                var availableLanguages = OcrEngine.AvailableRecognizerLanguages;
                return availableLanguages.Count > 0 ? availableLanguages[0] : null;
            }
            catch
            {
                return null;
            }
        }
    }
}