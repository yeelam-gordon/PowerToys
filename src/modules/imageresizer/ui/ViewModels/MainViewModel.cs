#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using System.Linq;
using ImageResizer.Helpers;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Views;

namespace ImageResizer.ViewModels
{
    public class MainViewModel : Observable
    {
        private readonly Settings _settings;
        private readonly ResizeBatch _batch;

        private object _currentPage;
        private double _progress;

        public MainViewModel(ResizeBatch batch, Settings settings)
        {
            _batch = batch;
            _settings = settings;
            LoadCommand = new RelayCommand<IMainView>(Load);
        }

        public ICommand LoadCommand { get; }

        public object CurrentPage
        {
            get => _currentPage;
            set => Set(ref _currentPage, value);
        }

        public double Progress
        {
            get => _progress;
            set => Set(ref _progress, value);
        }

        public void Load(IMainView view)
        {
            if (_batch.Files.Count == 0)
            {
                _batch.Files.AddRange(view.OpenPictureFiles());
            }

            // Check if any input files are HEIC/HEIF
            bool hasHeicFiles = _batch.Files.Any(file => 
                Path.GetExtension(file).Equals(".heic", System.StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(file).Equals(".heif", System.StringComparison.OrdinalIgnoreCase));

            // If there are HEIC files and we haven't prompted yet, check if the codec is installed
            if (hasHeicFiles && !HEICHelper.IsHEICCodecInstalled() && !_settings.PromptedForHEICCodec)
            {
                HEICHelper.PromptToInstallHEICCodec();
            }

            CurrentPage = new InputViewModel(_settings, this, view, _batch);
        }
    }
}
