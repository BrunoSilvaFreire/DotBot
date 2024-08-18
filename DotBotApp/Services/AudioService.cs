using System.Diagnostics;
using Discord;
using Discord.Audio;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace DotBot.Services;

public abstract class VideoPlaybackState;

public class QueryingVideo : VideoPlaybackState
{
    public QueryingVideo(VideoId videoId)
    {
        VideoId = videoId;
    }

    public VideoId VideoId { get; }
}

public class QueryingManifest : VideoPlaybackState
{
    public QueryingManifest(Video video)
    {
        Video = video;
    }

    public Video Video { get; }
}

public class NoCandidateStream : VideoPlaybackState
{
    public NoCandidateStream(IReadOnlyList<IStreamInfo> streams)
    {
        Streams = streams;
    }

    public IReadOnlyList<IStreamInfo> Streams { get; }
}

public class SelectingStream : VideoPlaybackState
{
    public SelectingStream(IReadOnlyList<IStreamInfo> streams)
    {
        Streams = streams;
    }

    public IReadOnlyList<IStreamInfo> Streams { get; }
}

public class Playing : VideoPlaybackState
{
    public Playing(IStreamInfo streamInfo)
    {
        StreamInfo = streamInfo;
    }

    public IStreamInfo StreamInfo { get; }
}

public class AudioTranspilationExecutor
{
    private Stream _inputAudioWebm;
    private Stream _outputAudio;
    private bool _isRunning;
    private TaskCompletionSource<bool> _onCompletion;

    public AudioTranspilationExecutor(Stream inputAudioWebm, Stream outputAudio,
        TaskCompletionSource<bool> onCompletion)
    {
        _onCompletion = onCompletion;
        _inputAudioWebm = inputAudioWebm;
        _outputAudio = outputAudio;
        _isRunning = false;
    }

    private Process? StartFFmpeg(string outPath)
    {
        var ffmpegProccess = new Process();
        var startInfo = ffmpegProccess.StartInfo;
        startInfo.FileName = "ffmpeg";
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;

        var args = new[]
        {
            "-loglevel", "debug",
            "-i",
            "pipe:",
            "-ac", "2",
            "-f",
            "s16le",
            "-y",
            outPath
        };
        startInfo.Arguments = string.Join(" ", args);
        Console.WriteLine($"Invoking process: {startInfo.FileName} {startInfo.Arguments}");
        if (!ffmpegProccess.Start())
        {
            return null;
        }

        return ffmpegProccess;
    }


    public void Run()
    {
        _isRunning = true;
        const int bufferSize = 4096;
        var buffer = new byte[bufferSize];

        var success = false;
        var path = Path.Join(Path.GetTempPath(), "temp.pcm");
        var ffmpeg = StartFFmpeg(path);
        if (ffmpeg != null)
        {
            success = true;
            _inputAudioWebm.Seek(0, SeekOrigin.Begin);
            try
            {
                _inputAudioWebm.CopyTo(ffmpeg.StandardInput.BaseStream);
            }
            finally
            {
                ffmpeg.StandardInput.BaseStream.Close();
                ffmpeg.StandardOutput.BaseStream.Close();
                ffmpeg.WaitForExit();
            }

            using var stream = new FileStream(path, FileMode.Open);
            stream.CopyTo(_outputAudio);
            Console.WriteLine("Copied to audio stream");
        }

        _onCompletion.TrySetResult(success);
    }

    public void Stop()
    {
        _isRunning = false;
    }
}

public class AudioPlayer : IAsyncDisposable
{
    private IAudioChannel _channel;
    private YoutubeClient _youtubeClient;
    private IAudioClient? _currentAudioClient;
    private AudioOutStream? _pcmOutputStream;
    private AudioTranspilationExecutor? _executor;

    public AudioPlayer(IAudioChannel channel)
    {
        _channel = channel;
        _youtubeClient = new YoutubeClient();
    }

    IStreamInfo? SelectStreamInfo(StreamManifest manifest)
    {
        return manifest
            .GetAudioOnlyStreams()
            .Where(info => info.AudioCodec == "opus")
            .TryGetWithHighestBitrate();
    }

    public async IAsyncEnumerable<VideoPlaybackState> PlayYoutubeImmediately(string link)
    {
        StopIfPlaying();
        if (_pcmOutputStream == null)
        {
            yield break;
        }

        var candidateVideoId = VideoId.TryParse(link);
        if (candidateVideoId == null)
        {
            yield break;
        }

        var videoId = candidateVideoId.Value;
        yield return new QueryingVideo(videoId);
        var video = await _youtubeClient.Videos.GetAsync(videoId);
        yield return new QueryingManifest(video);
        var manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
        yield return new SelectingStream(manifest.Streams);
        var best = SelectStreamInfo(manifest);
        if (best == null)
        {
            yield return new NoCandidateStream(manifest.Streams);
        }
        else
        {
            Stream stream;
            {
                var cancellationToken = new CancellationToken();
                stream = await _youtubeClient.Videos.Streams.GetAsync(best, cancellationToken);
            }
            yield return new Playing(best);
            await PlayStream(stream);
        }
    }

    public async Task PlayStream(Stream opusStream)
    {
        var taskCompletionSource = new TaskCompletionSource<bool>();
        Debug.Assert(_pcmOutputStream != null, nameof(_pcmOutputStream) + " != null");
        _executor = new AudioTranspilationExecutor(
            opusStream,
            _pcmOutputStream,
            taskCompletionSource
        );
        var thread = new Thread(_executor.Run);
        thread.Start();
        await _pcmOutputStream.FlushAsync();
        await taskCompletionSource.Task;
    }

    private void StopIfPlaying()
    {
        _executor?.Stop();
    }

    public async Task Initialize()
    {
        _currentAudioClient = await _channel.ConnectAsync();
        _pcmOutputStream = _currentAudioClient.CreatePCMStream(AudioApplication.Mixed);
    }

    public async ValueTask DisposeAsync()
    {
        if (_pcmOutputStream != null)
        {
            await _pcmOutputStream.DisposeAsync();
        }

        if (_currentAudioClient != null)
        {
            if (_currentAudioClient is IAsyncDisposable currentAudioClientAsyncDisposable)
            {
                await currentAudioClientAsyncDisposable.DisposeAsync();
            }
            else
            {
                _currentAudioClient?.Dispose();
            }
        }
    }
}

public class AudioService
{
    private Dictionary<IAudioChannel, AudioPlayer> _existingAudioPlayers = new Dictionary<IAudioChannel, AudioPlayer>();

    public async Task<AudioPlayer?> GetAudioPlayer(IAudioChannel channel)
    {
        if (_existingAudioPlayers.TryGetValue(channel, out var audioPlayer))
        {
            return audioPlayer;
        }

        var player = new AudioPlayer(channel);
        await player.Initialize();
        _existingAudioPlayers.Add(channel, player);
        return player;
    }

    public async Task CleanupAudioPlayerFor(IAudioChannel channel)
    {
        if (_existingAudioPlayers.TryGetValue(channel, out var audioPlayer))
        {
            await audioPlayer.DisposeAsync();
            _existingAudioPlayers.Remove(channel);
        }
    }
}