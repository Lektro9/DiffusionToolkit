﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using System.Windows.Threading;

namespace Diffusion.Toolkit.Thumbnails;

public class ThumbnailLoader
{
    private readonly Dispatcher _dispatcher;
    private static ThumbnailLoader? _instance;
    private readonly Channel<Job<ThumbnailJob, BitmapSource>> _channel = Channel.CreateUnbounded<Job<ThumbnailJob, BitmapSource>>();
    private readonly int _degreeOfParallelism = 2;

    private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    private Stream _defaultStream;

    private ThumbnailLoader(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;

        _defaultStream = new MemoryStream();

        StreamResourceInfo sri = Application.GetResourceStream(new Uri("Images/thumbnail.png", UriKind.Relative));
        if (sri != null)
        {
            using (Stream s = sri.Stream)
            {
                sri.Stream.CopyTo(_defaultStream);
                _defaultStream.Position = 0;
            }
        }
    }

    public static void CreateInstance(Dispatcher dispatcher)
    {
        _instance = new ThumbnailLoader(dispatcher);
    }

    public static ThumbnailLoader Instance => _instance;

    public void Stop()
    {
        cancellationTokenSource.Cancel();
        _channel.Writer.Complete();
    }

    public Task Start()
    {
        var consumers = new List<Task>();

        for (var i = 0; i < _degreeOfParallelism; i++)
        {
            consumers.Add(ProcessTaskAsync(cancellationTokenSource.Token));
        }

        //_channel.Writer.Complete();

        return Task.WhenAll(consumers);
    }

    public Task StartRun()
    {
        var consumers = new List<Task>();

        for (var i = 0; i < _degreeOfParallelism; i++)
        {
            consumers.Add(Task.Run(async () => await ProcessTaskAsync(cancellationTokenSource.Token)));
        }

        //_channel.Writer.Complete();

        return Task.WhenAll(consumers);
    }

    private long _requestId;

    public void SetCurrentRequestId(long requestId)
    {
        _requestId = requestId;
    }

    private async Task ProcessTaskAsync(CancellationToken token)
    {

        while (await _channel.Reader.WaitToReadAsync(token))
        {
            var job = await _channel.Reader.ReadAsync(token);

            if (job.Data.RequestId != _requestId)
            {
                continue;
            }

            if (!ThumbnailCache.Instance.TryGetThumbnail(job.Data.Path,
                    out BitmapSource? thumbnail))
            {
                // Debug.WriteLine($"Loading from disk");

                thumbnail = GetThumbnailImmediate(job.Data.Path, job.Data.Width, job.Data.Height);
                ThumbnailCache.Instance.AddThumbnail(job.Data.Path, thumbnail);

                _dispatcher.Invoke(() =>
                {
                    job.Completion(thumbnail);
                });
            }
            else
            {
                _dispatcher.Invoke(() =>
                {
                    job.Completion(thumbnail);
                });
            }
        }
    }

    public async Task QueueAsync(ThumbnailJob job, Action<BitmapSource> completion)
    {
        await _channel.Writer.WriteAsync(new Job<ThumbnailJob, BitmapSource>() { Data = job, Completion = completion });
    }

    private static Stream GenerateThumbnail(string path, int width, int height)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        var bitmap = new BitmapImage();
        //bitmap.UriSource = new Uri(path);
        bitmap.BeginInit();

        var size = 128;

        if (width > height)
        {
            bitmap.DecodePixelWidth = size;
        }
        else
        {
            bitmap.DecodePixelHeight = size;
        }
        //bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();

        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap, null, null, null));

        var ministream = new MemoryStream();

        encoder.Save(ministream);

        ministream.Seek(0, SeekOrigin.Begin);

        return ministream;
    }

    private BitmapImage GetThumbnailImmediate(string path, int width, int height)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        if (File.Exists(path))
        {
            bitmap.StreamSource = GenerateThumbnail(path, width, height);
        }
        else
        {
            bitmap.StreamSource = _defaultStream;
        }
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }


    private static BitmapImage GetThumbnail(string path, int width, int height)
    {
        BitmapImage bitmap = null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        bitmap = new BitmapImage();
        //bitmap.UriSource = new Uri(path);
        bitmap.BeginInit();

        var size = 128;
        bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
        if (width > height)
        {
            bitmap.DecodePixelWidth = size;
        }
        else
        {
            bitmap.DecodePixelHeight = size;
        }
        //bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

}