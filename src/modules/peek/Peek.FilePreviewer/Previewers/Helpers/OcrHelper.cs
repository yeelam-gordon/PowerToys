// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI.Xaml.Media.Imaging;
using Peek.FilePreviewer.Models;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.System.UserProfile;

namespace Peek.FilePreviewer.Previewers.Helpers
{
    public static class OcrHelper
    {
        /// <summary>
        /// Extract text from the specified point in an image file
        /// </summary>
        /// <param name="imagePath">The path to the image file</param>
        /// <param name="clickPoint">The point where user clicked/double-clicked</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The extracted text and its bounding rectangle at the specified point</returns>
        public static async Task<TextExtractionResult> ExtractTextAtPointFromFileAsync(string imagePath, Windows.Foundation.Point clickPoint, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                return new TextExtractionResult();
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Check for unsupported file types
                var extension = Path.GetExtension(imagePath).ToLowerInvariant();
                if (extension == ".svg" || extension == ".qoi")
                {
                    // SVG and QOI files are not directly supported by BitmapDecoder for OCR
                    return new TextExtractionResult();
                }

                // Load image file as SoftwareBitmap
                var storageFile = await StorageFile.GetFileFromPathAsync(imagePath);
                
                cancellationToken.ThrowIfCancellationRequested();

                using var stream = await storageFile.OpenAsync(FileAccessMode.Read);
                var decoder = await BitmapDecoder.CreateAsync(stream);
                var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                cancellationToken.ThrowIfCancellationRequested();

                // Get OCR language
                var ocrLanguage = GetOcrLanguage();
                if (ocrLanguage == null)
                {
                    return new TextExtractionResult();
                }

                // Create OCR engine
                var ocrEngine = OcrEngine.TryCreateFromLanguage(ocrLanguage);
                if (ocrEngine == null)
                {
                    ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
                    if (ocrEngine == null)
                    {
                        return new TextExtractionResult();
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
                            return new TextExtractionResult(word.Text, word.BoundingRect);
                        }
                    }
                }

                return new TextExtractionResult();
            }
            catch (Exception)
            {
                // Return empty result on any error
                return new TextExtractionResult();
            }
        }

        /// <summary>
        /// Extract text from the specified point in an image
        /// </summary>
        /// <param name="imageSource">The image source to extract text from</param>
        /// <param name="clickPoint">The point where user clicked/double-clicked</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The extracted text and its bounding rectangle at the specified point</returns>
        public static async Task<TextExtractionResult> ExtractTextAtPointAsync(BitmapSource imageSource, Windows.Foundation.Point clickPoint, CancellationToken cancellationToken = default)
        {
            if (imageSource == null)
            {
                return new TextExtractionResult();
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
                    return new TextExtractionResult();
                }

                // Create OCR engine
                var ocrEngine = OcrEngine.TryCreateFromLanguage(ocrLanguage);
                if (ocrEngine == null)
                {
                    ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
                    if (ocrEngine == null)
                    {
                        return new TextExtractionResult();
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
                            return new TextExtractionResult(word.Text, word.BoundingRect);
                        }
                    }
                }

                return new TextExtractionResult();
            }
            catch (Exception)
            {
                // Return empty result on any error
                return new TextExtractionResult();
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

            // Simple approach: if it's a WriteableBitmap, we can access pixel buffer directly
            if (bitmapSource is WriteableBitmap writeableBitmap)
            {
                return SoftwareBitmap.CreateCopyFromBuffer(
                    writeableBitmap.PixelBuffer,
                    BitmapPixelFormat.Bgra8,
                    writeableBitmap.PixelWidth,
                    writeableBitmap.PixelHeight);
            }

            // For other types, throw an exception as this is a fallback method
            throw new NotSupportedException("This BitmapSource type is not supported for OCR. Use file-based method instead.");
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