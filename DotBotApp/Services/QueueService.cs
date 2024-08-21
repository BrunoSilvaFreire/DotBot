using System.Runtime.InteropServices;

namespace DotBot.Services;

public class QueueService
{

    private  List<string> queue = [];
    public void AddToQueue(string youtubeLink)
    {
        this.queue.Add(youtubeLink);
    }

    public bool IsQueueEmpty ()
    {
        return queue.Count == 0;
    }

    public string NextSong ()
    {
        string nextSong = this.queue[0];
        this.queue.RemoveAt(0);
        return nextSong;
    }
}

