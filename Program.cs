using System;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;

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
            var comments = await fetcher.GetVideoCommentsStream(videoId, 100).ToList();
            var sentimentAnalysisResult = sentimentService.AnalyzeSentiment(comments);

            // Serialize sentiment analysis result to JSON
            var jsonResponse = JsonConvert.SerializeObject(sentimentAnalysisResult);

            // Write JSON response to the client
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json";
            await WriteResponseAsync(context.Response, jsonResponse);
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

    static void LogResponse(HttpListenerResponse response, bool success, Exception? ex)
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
