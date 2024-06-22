using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reactive.Linq;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

public class YouTubeCommentFetcher
{
    private readonly string _apiKey;

    public YouTubeCommentFetcher(string apiKey)
    {
        _apiKey = apiKey;
    }

    // observable sequence za fetch komentara
    public IObservable<string?> GetVideoCommentsStream(string videoId, int maxResults = 1000)
    {
        return Observable.Create<string?>(async observer =>
        {
            using (var httpClient = new HttpClient())
            {
                string? nextPageToken = null;
                while (true)
                {
                    var url = $"https://www.googleapis.com/youtube/v3/commentThreads?part=snippet&videoId={videoId}&key={_apiKey}&maxResults=100";
                    if (nextPageToken != null)
                    {
                        url += $"&pageToken={nextPageToken}";
                    }

                    var response = await httpClient.GetStringAsync(url);
                    var jsonResponse = JObject.Parse(response);

                    foreach (var item in jsonResponse["items"])
                    {
                        var comment = item["snippet"]["topLevelComment"]["snippet"]["textOriginal"].ToString();
                        observer.OnNext(comment);
                        if (--maxResults == 0)
                        {
                            observer.OnCompleted();
                            return;
                        }
                    }

                    nextPageToken = jsonResponse.Value<string?>("nextPageToken");
                    if (nextPageToken == null)
                    {
                        observer.OnCompleted();
                        return;
                    }
                }
            }
        });
    }
}
