// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using Windows.System.UserProfile;

namespace Peek.FilePreviewer.Previewers.Helpers
{
    public static class OcrHelper
    {
        /// <summary>
        /// Extract text from the specified point in an image
        /// </summary>
        /// <param name="imageSource">The image source to extract text from</param>
        /// <param name="clickPoint">The point where user clicked/double-clicked</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The extracted text at the specified point</returns>
        public static async Task<string> ExtractTextAtPointAsync(BitmapSource imageSource, Windows.Foundation.Point clickPoint, CancellationToken cancellationToken = default)
        {
            if (imageSource == null)
            {
                return string.Empty;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Convert BitmapSource to SoftwareBitmap for OCR
                var softwareBitmap = await ConvertToSoftwareBitmapAsync(imageSource, cancellationToken);
                
                cancellationToken.ThrowIfCancellationRequested();

                // Get OCR language
                var ocrLanguage = GetOcrLanguage();
                if (ocrLanguage == null)
                {
                    return string.Empty;
                }

                // Create OCR engine
                var ocrEngine = OcrEngine.TryCreateFromLanguage(ocrLanguage);
                if (ocrEngine == null)
                {
                    ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
                    if (ocrEngine == null)
                    {
                        return string.Empty;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Perform OCR
                var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap);
                
                // Find text at the clicked point
                foreach (var line in ocrResult.Lines)
                {
                    foreach (var word in line.Words)
                    {
                        if (word.BoundingRect.Contains(clickPoint))
                        {
                            return word.Text;
                        }
                    }
                }

                return string.Empty;
            }
            catch (Exception)
            {
                // Return empty string on any error
                return string.Empty;
            }
        }

        /// <summary>
        /// Extract all text from an image
        /// </summary>
        /// <param name="imageSource">The image source to extract text from</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>All extracted text from the image</returns>
        public static async Task<string> ExtractAllTextAsync(BitmapSource imageSource, CancellationToken cancellationToken = default)
        {
            if (imageSource == null)
            {
                return string.Empty;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Convert BitmapSource to SoftwareBitmap for OCR
                var softwareBitmap = await ConvertToSoftwareBitmapAsync(imageSource, cancellationToken);
                
                cancellationToken.ThrowIfCancellationRequested();

                // Get OCR language
                var ocrLanguage = GetOcrLanguage();
                if (ocrLanguage == null)
                {
                    return string.Empty;
                }

                // Create OCR engine
                var ocrEngine = OcrEngine.TryCreateFromLanguage(ocrLanguage);
                if (ocrEngine == null)
                {
                    ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
                    if (ocrEngine == null)
                    {
                        return string.Empty;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Perform OCR
                var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap);
                
                return ocrResult.Text ?? string.Empty;
            }
            catch (Exception)
            {
                // Return empty string on any error
                return string.Empty;
            }
        }

        private static async Task<SoftwareBitmap> ConvertToSoftwareBitmapAsync(BitmapSource bitmapSource, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Convert BitmapSource to stream, then to SoftwareBitmap
            using var memoryStream = new MemoryStream();
            
            // Create a RenderTargetBitmap to get pixel data
            var renderTargetBitmap = new RenderTargetBitmap();
            
            // We need an Image element to render the BitmapSource
            var imageElement = new Microsoft.UI.Xaml.Controls.Image
            {
                Source = bitmapSource
            };

            await renderTargetBitmap.RenderAsync(imageElement);
            
            cancellationToken.ThrowIfCancellationRequested();

            // Get pixels from RenderTargetBitmap
            var pixelBuffer = await renderTargetBitmap.GetPixelsAsync();
            
            // Create SoftwareBitmap from pixel buffer
            return SoftwareBitmap.CreateCopyFromBuffer(
                pixelBuffer,
                BitmapPixelFormat.Bgra8,
                renderTargetBitmap.PixelWidth,
                renderTargetBitmap.PixelHeight);
        }

        private static Language GetOcrLanguage()
        {
            var userLanguageTags = GlobalizationPreferences.Languages.ToList();

            var languages = from language in OcrEngine.AvailableRecognizerLanguages
                            let tag = language.LanguageTag
                            where userLanguageTags.Contains(tag)
                            orderby userLanguageTags.IndexOf(tag)
                            select language;

            return languages.FirstOrDefault();
        }
    }
}