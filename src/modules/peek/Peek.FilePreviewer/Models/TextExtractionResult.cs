// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Peek.FilePreviewer.Models
{
    /// <summary>
    /// Represents the result of text extraction from an image, including the extracted text and its bounding rectangle
    /// </summary>
    public class TextExtractionResult
    {
        /// <summary>
        /// The extracted text
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// The bounding rectangle of the extracted text in image coordinates
        /// </summary>
        public Rect BoundingRect { get; set; }

        /// <summary>
        /// Indicates whether text was successfully extracted
        /// </summary>
        public bool HasText => !string.IsNullOrWhiteSpace(Text);

        public TextExtractionResult()
        {
        }

        public TextExtractionResult(string text, Rect boundingRect)
        {
            Text = text;
            BoundingRect = boundingRect;
        }
    }
}