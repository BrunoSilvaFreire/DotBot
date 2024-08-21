using Discord;
using Discord.Interactions;
using DotBot.Services;
using ContextType = Discord.Commands.ContextType;

namespace DotBot.Modules;

public class MusicModule : InteractionModuleBase
{
    private AudioService _audioService;
    private QueueService _queueService;

    public MusicModule(AudioService audioService, QueueService queueService)
    {
        _audioService = audioService;
        _queueService = queueService;
    }



    [SlashCommand("play", "Play a song from youtube.", runMode: RunMode.Async)]
    [Discord.Commands.RequireContext(ContextType.Guild)]
    private async Task Play(string youtubeLink)
    {
        await DeferAsync(true);

        if (Context.User is not IGuildUser user)
        {
            await RespondAsync("User is not in a guild.", ephemeral: true);
            return;
        }

        if (user.VoiceChannel == null)
        {
            await RespondAsync("You are not in a voice channel.", ephemeral: true);
            return;
        }

        var player = await _audioService.GetAudioPlayer(user.VoiceChannel);
        if (player == null)
        {
            await RespondAsync("Unable to load audio player for your voice channel 🤔.", ephemeral: true);
            return;
        }
        if (player.IsCurrentlyPlaying)
        {
            _queueService.AddToQueue(youtubeLink);
            await ReplyAsync($"This music was add to the queue");
        }
        else
        {
            await foreach (var state in player.PlayYoutubeImmediately(youtubeLink))
            {
                switch (state)
                {
                    case QueryingVideo queryingVideo:
                        await FollowupAsync($"Querying video {queryingVideo.VideoId}", ephemeral: true);
                        break;
                    case QueryingManifest queryingManifest:
                        await ModifyOriginalResponseAsync(
                            properties =>
                            {
                                properties.Content =
                                    $"Querying manifest {queryingManifest.Video.Id} ({queryingManifest.Video.Title})";
                            });
                        break;
                    case NoCandidateStream _:
                        await ModifyOriginalResponseAsync(properties =>
                            properties.Content = "No candidate stream found (DotBot only supports opus streams)."
                        );
                        await _audioService.CleanupAudioPlayerFor(user.VoiceChannel);
                        break;
                    case SelectingStream selectingStream:
                        await ModifyOriginalResponseAsync(properties =>
                        {
                            properties.Content = $"Selecting stream from {selectingStream.Streams.Count} streams";
                        });
                        break;
                    case Playing playing:
                        await ModifyOriginalResponseAsync(
                            properties =>
                                properties.Content =
                                    $"Playing from stream {playing.StreamInfo} (bitrate: {playing.StreamInfo.Bitrate}, filesize: {playing.StreamInfo.Size}, container: {playing.StreamInfo.Container})"
                        );
                        break;
                }
            }

            await _audioService.CleanupAudioPlayerFor(user.VoiceChannel);
            await RespondAsync($"Finished playing {youtubeLink}");

            string nextSong = _queueService.RemoveFromQueue();
            
            await Play(nextSong);
            
        }

    }

    [SlashCommand("debugplay", "Play a song from the host's file system.", runMode: RunMode.Async)]
    [Discord.Commands.RequireContext(ContextType.Guild)]
    private async Task DebugPlay(string filePath)
    {
        await DeferAsync(true);

        if (Context.User is not IGuildUser user)
        {
            await RespondAsync("User is not in a guild.", ephemeral: true);
            return;
        }

        if (user.VoiceChannel == null)
        {
            await RespondAsync("You are not in a voice channel.", ephemeral: true);
            return;
        }

        var player = await _audioService.GetAudioPlayer(user.VoiceChannel);
        if (player == null)
        {
            await RespondAsync("Unable to load audio player for your voice channel 🤔.", ephemeral: true);
            return;
        }

        var absolutePath = Path.GetFullPath(filePath);
        if (!File.Exists(absolutePath))
        {
            await RespondAsync("File does not exist.", ephemeral: true);
            return;
        }

        var inMemoryStream = new MemoryStream();
        await using (var fileStream = File.OpenRead(absolutePath))
        {
            await fileStream.CopyToAsync(inMemoryStream);
        }

        await player.PlayStream(inMemoryStream);
        await _audioService.CleanupAudioPlayerFor(user.VoiceChannel);
    }
}