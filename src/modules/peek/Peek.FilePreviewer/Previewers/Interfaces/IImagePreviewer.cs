// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Peek.FilePreviewer.Previewers.Interfaces
{
    public interface IImagePreviewer : IPreviewer, IPreviewTarget
    {
        public ImageSource? Preview { get; }

        public double ScalingFactor { get; set; }

        public Size MaxImageSize { get; set; }

        /// <summary>
        /// Extract text from a specific point in the image
        /// </summary>
        /// <param name="clickPoint">The point where user clicked</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The extracted text at the specified point</returns>
        public Task<string> ExtractTextAtPointAsync(Point clickPoint, CancellationToken cancellationToken = default);
    }
}
