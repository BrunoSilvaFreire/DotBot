using System.Runtime.InteropServices;

namespace DotBot.Services;

public class QueueService
{

    private  List<string> queue = [];
    public void AddToQueue(string youtubeLink)
    {
        this.queue.Add(youtubeLink);
    }

    public string RemoveFromQueue()
    {
        string nextSong = this.queue[0];
        this.queue.RemoveAt(0);

        return nextSong;
    }
}

