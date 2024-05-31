using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Newtonsoft.Json;

class Program
{
    static void Main(string[] args)
    {
        var apiKey = "AIzaSyD6RwDuBUu70dE_ZajwD7mbokp5Csu3gEQ";
        var fetcher = new YouTubeCommentFetcher(apiKey);
        var sentimentService = new SentimentAnalysisService();

        var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8080/");
        listener.Start();
        Console.WriteLine("Listening...");

        var requestStream = Observable.FromAsync(listener.GetContextAsync)
            .Repeat()
            .Publish()
            .RefCount();

        var scheduler = new EventLoopScheduler();

        requestStream
            .ObserveOn(scheduler)
            .Subscribe(async context =>
            {
                LogRequest(context.Request);
                await HandleRequest(context, fetcher, sentimentService);
            },
            ex => Console.WriteLine($"Error: {ex.Message}"),
            () => Console.WriteLine("Completed"));

        Console.ReadLine();
        listener.Stop();
    }

    static void LogRequest(HttpListenerRequest request)
    {
        Console.WriteLine($"Received request for {request.Url}");
    }

    static async Task HandleRequest(HttpListenerContext context, YouTubeCommentFetcher fetcher, SentimentAnalysisService sentimentService)
    {
        try
        {
            var query = context.Request.QueryString;
            var videoId = query["videoId"];
            if (string.IsNullOrEmpty(videoId))
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await WriteResponseAsync(context.Response, "Missing videoId parameter");
                return;
            }

            Console.WriteLine($"Fetching comments for video ID: {videoId}");
            var comments = await fetcher.GetVideoCommentsAsync(videoId, 1000); // Prikupljamo 1000 komentara
            var sentiments = sentimentService.AnalyzeSentiment(comments);
            var averageSentiment = sentiments.Sum(data => data.Score) / sentiments.Count;

            // Pronalaženje najpozitivnijeg i najnegativnijeg komentara
            var mostPositiveComment = sentiments.OrderByDescending(data => data.Score).FirstOrDefault();
            var mostNegativeComment = sentiments.OrderBy(data => data.Score).FirstOrDefault();

            var responseString = JsonConvert.SerializeObject(new
            {
                Comments = sentiments,
                AverageSentiment = averageSentiment,
                MostPositiveComment = mostPositiveComment,
                MostNegativeComment = mostNegativeComment
            }, Formatting.Indented);

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await WriteResponseAsync(context.Response, responseString);
            LogResponse(context.Response, true, null);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await WriteResponseAsync(context.Response, $"Error: {ex.Message}");
            LogResponse(context.Response, false, ex);
        }
    }

    static async Task WriteResponseAsync(HttpListenerResponse response, string responseString)
    {
        var buffer = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = buffer.Length;
        var responseOutput = response.OutputStream;
        await responseOutput.WriteAsync(buffer, 0, buffer.Length);
        responseOutput.Close();
    }

    static void LogResponse(HttpListenerResponse response, bool success, Exception ex)
    {
        if (success)
        {
            Console.WriteLine($"Response sent with status {response.StatusCode}");
        }
        else
        {
            Console.WriteLine($"Failed to process request: {ex.Message}");
        }
    }
}
