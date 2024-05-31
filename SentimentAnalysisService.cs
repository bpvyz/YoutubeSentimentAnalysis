using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class SentimentAnalysisService
{
    private readonly MLContext _mlContext;
    private readonly PredictionEngine<SentimentData, SentimentPrediction> _predictionEngine;

    public SentimentAnalysisService()
    {
        _mlContext = new MLContext();
        var model = TrainModel();
        _predictionEngine = _mlContext.Model.CreatePredictionEngine<SentimentData, SentimentPrediction>(model);
    }

    private ITransformer TrainModel()
    {
        string dir = System.IO.Directory.GetCurrentDirectory();
        var data = _mlContext.Data.LoadFromTextFile<SentimentData>(dir + "\\DataSet.csv", separatorChar: ',', hasHeader: true);

        var pipeline = _mlContext.Transforms.Text.FeaturizeText("Features", nameof(SentimentData.SentimentText))
            .Append(_mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(labelColumnName: nameof(SentimentData.Sentiment), featureColumnName: "Features"));

        return pipeline.Fit(data);
    }

    public List<SentimentData> AnalyzeSentiment(List<string> comments)
    {
        var sentimentDataList = comments.Select(comment =>
        {
            var prediction = _predictionEngine.Predict(new SentimentData { SentimentText = comment });
            return new SentimentData { SentimentText = comment, Sentiment = prediction.Prediction, Score = prediction.Score };
        }).ToList();

        var totalScore = sentimentDataList.Sum(data => data.Score);
        var averageScore = totalScore / sentimentDataList.Count;

        // print each comment with its sentiment data
        foreach (var data in sentimentDataList)
        {
            Console.WriteLine($"Comment: {data.SentimentText}");
            Console.WriteLine($"Sentiment: {data.Sentiment} - Score: {data.Score}");
            Console.WriteLine();
        }

        Console.WriteLine($"Average Sentiment: {averageScore}");

        return sentimentDataList;
    }
}

public class SentimentData
{
    [LoadColumn(0)]
    public float ItemID { get; set; }

    [LoadColumn(1)]
    public bool Sentiment { get; set; }

    [LoadColumn(2)]
    public string SentimentText { get; set; }

    [LoadColumn(3)]
    public float Score { get; set; }
}

public class SentimentPrediction
{
    [ColumnName("PredictedLabel")]
    public bool Prediction { get; set; }
    public float Score { get; set; }
}
