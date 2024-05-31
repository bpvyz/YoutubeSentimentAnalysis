using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public class YouTubeCommentFetcher
{
    private readonly string _apiKey;

    public YouTubeCommentFetcher(string apiKey)
    {
        _apiKey = apiKey;
    }

    public async Task<List<string>> GetVideoCommentsAsync(string videoId, int maxResults = 1000)
    {
        var comments = new List<string>();
        using (var httpClient = new HttpClient())
        {
            string nextPageToken = null;
            while (comments.Count < maxResults)
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
                    comments.Add(comment);
                    if (comments.Count >= maxResults)
                    {
                        break;
                    }
                }

                nextPageToken = jsonResponse.Value<string>("nextPageToken");
                if (nextPageToken == null)
                {
                    break;
                }
            }
        }
        return comments;
    }
}
