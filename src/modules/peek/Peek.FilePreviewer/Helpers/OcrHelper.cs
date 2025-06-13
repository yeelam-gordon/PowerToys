// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ManagedCommon;
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
        /// Extract text from a specific point in an image with proper aspect ratio scaling
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="clickPoint">Point where user clicked relative to the image control</param>
        /// <param name="imageControlSize">Size of the image control displaying the image</param>
        /// <param name="actualImageSize">Actual size of the image file</param>
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
                
                // Calculate scaling factors accounting for aspect ratio preservation
                // The Image control preserves aspect ratio, so we need to find the actual displayed image bounds
                var imageAspectRatio = actualImageSize.Width / actualImageSize.Height;
                var controlAspectRatio = imageControlSize.Width / imageControlSize.Height;
                
                double displayedWidth, displayedHeight;
                double offsetX = 0, offsetY = 0;
                
                if (imageAspectRatio > controlAspectRatio)
                {
                    // Image is wider - fit to control width, center vertically
                    displayedWidth = imageControlSize.Width;
                    displayedHeight = imageControlSize.Width / imageAspectRatio;
                    offsetY = (imageControlSize.Height - displayedHeight) / 2;
                }
                else
                {
                    // Image is taller - fit to control height, center horizontally
                    displayedHeight = imageControlSize.Height;
                    displayedWidth = imageControlSize.Height * imageAspectRatio;
                    offsetX = (imageControlSize.Width - displayedWidth) / 2;
                }
                
                // Check if click is within the actual image bounds
                if (clickPoint.X < offsetX || clickPoint.X > offsetX + displayedWidth ||
                    clickPoint.Y < offsetY || clickPoint.Y > offsetY + displayedHeight)
                {
                    return string.Empty; // Click is outside the image
                }
                
                // Scale the click point to image coordinates
                var relativeX = clickPoint.X - offsetX;
                var relativeY = clickPoint.Y - offsetY;
                var scaleX = actualImageSize.Width / displayedWidth;
                var scaleY = actualImageSize.Height / displayedHeight;
                
                var scaledPoint = new Windows.Foundation.Point(
                    relativeX * scaleX,
                    relativeY * scaleY);
                
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
            catch (Exception ex)
            {
                // Log the specific error for debugging
                Logger.LogError($"OCR text extraction failed: {ex.Message}", ex);
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
            catch (Exception ex)
            {
                Logger.LogError($"Failed to get OCR language: {ex.Message}", ex);
                return null;
            }
        }
    }
}