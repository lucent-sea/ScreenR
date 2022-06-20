﻿using Microsoft.Extensions.Logging;
using Microsoft.IO;
using ScreenR.Desktop.Control.Interfaces;
using ScreenR.Desktop.Control.Services;
using ScreenR.Shared.Dtos;
using ScreenR.Shared.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ScreenR.Desktop.Windows.Capture
{
    internal class DesktopStreamer : IDesktopStreamer
    {
        private readonly RecyclableMemoryStreamManager _streamManager = new();
        private readonly IScreenGrabber _screenGrabber;
        private readonly ILogger<DesktopStreamer> _logger;
        private readonly IBitmapUtility _bitmapUtility;
        private string _activeDisplay;
        private SKEncodedImageFormat _encodingFormat = SKEncodedImageFormat.Jpeg;
        private int _quality = 75;
        private bool _forceFullscreen = true;

        public DesktopStreamer(
            IScreenGrabber screenGrabber,
            IBitmapUtility bitmapUtility,
            ILogger<DesktopStreamer> logger)
        {
            _screenGrabber = screenGrabber;
            _logger = logger;
            _bitmapUtility = bitmapUtility;

            _activeDisplay = _screenGrabber
                .GetDisplays()
                .FirstOrDefault(x => x.IsPrimary)
                ?.DeviceName ?? "";
        }

        public async IAsyncEnumerable<DesktopFrameChunk> GetDesktopStream(
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            var buffer = new byte[50_000];
            SKBitmap currentFrame = new();
            SKBitmap previousFrame = new();

            while (!cancellationToken.IsCancellationRequested)
            {
                using var encodeStream = _streamManager.GetStream();
                Rectangle diffArea;

                try
                {
                    Array.Clear(buffer);

                    previousFrame?.Dispose();
                    previousFrame = currentFrame;

                    var result = _screenGrabber.GetScreenGrab(_activeDisplay);
                    if (result.IsSuccess && result.Value is not null)
                    {
                        currentFrame = result.Value;
                    }
                    else
                    {
                        var err = result.Error ?? "Null frame returned.";
                        _logger.LogError("Screen grab failed.  Error: {msg}", err);
                    }

                    diffArea = _bitmapUtility.GetDiffArea(currentFrame, previousFrame, _forceFullscreen);
                    _forceFullscreen = false;

                    if (diffArea.IsEmpty)
                    {
                        continue;
                    }

                    using var cropped = _bitmapUtility.CropBitmap(currentFrame, diffArea);
                    cropped.Encode(encodeStream, _encodingFormat, _quality);
                    encodeStream.Seek(0, SeekOrigin.Begin);
                }
                catch (Exception ex)
                {
                    Array.Clear(buffer);
                    _logger.LogError(ex, "Error while streaming desktop.");
                    continue;
                }

                foreach (var chunk in encodeStream.ToArray().Chunk(50_000))
                {
                    var desktopChunk = new DesktopFrameChunk()
                    {
                        Area = diffArea,
                        ImageBytes = chunk
                    };

                    yield return desktopChunk;
                }

                var endChunk = new DesktopFrameChunk()
                {
                    EndOfFrame = true,
                    Area = diffArea
                };

                yield return endChunk;

                await Task.CompletedTask;
            }
        }

        public IEnumerable<DisplayInfo> GetDisplays()
        {
            return _screenGrabber.GetDisplays();
        }

        public void SetActiveDisplay(string deviceName)
        {
            _activeDisplay = deviceName;
        }
    }
}
