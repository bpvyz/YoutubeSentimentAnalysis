using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;

public class SentimentAnalysisService
{
    private readonly MLContext _mlContext;
    private readonly PredictionEngine<SentimentData, SentimentPrediction> _predictionEngine;
    private readonly Subject<SentimentAnalysisResult> _sentimentSubject;

    public IObservable<SentimentAnalysisResult> SentimentStream => _sentimentSubject;

    public SentimentAnalysisService()
    {
        _mlContext = new MLContext();
        var model = TrainModel();
        _predictionEngine = _mlContext.Model.CreatePredictionEngine<SentimentData, SentimentPrediction>(model);
        _sentimentSubject = new Subject<SentimentAnalysisResult>();
    }

    private ITransformer TrainModel()
    {
        string dir = Directory.GetCurrentDirectory();
        var data = _mlContext.Data.LoadFromTextFile<SentimentData>(Path.Combine(dir, "DataSet.csv"), separatorChar: ',', hasHeader: true);

        var pipeline = _mlContext.Transforms.Text.FeaturizeText("Features", nameof(SentimentData.SentimentText))
            .Append(_mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(labelColumnName: nameof(SentimentData.Sentiment), featureColumnName: "Features"));

        return pipeline.Fit(data);
    }

    public object AnalyzeSentiment(IList<string> comments)
    {
        var sentimentDataList = new List<SentimentAnalysisResult>();

        foreach (var comment in comments)
        {
            var prediction = _predictionEngine.Predict(new SentimentData { SentimentText = comment });
            var sentimentResult = new SentimentAnalysisResult
            {
                SentimentText = comment,
                Sentiment = prediction.Prediction,
                Score = prediction.Score
            };

            sentimentDataList.Add(sentimentResult);
            _sentimentSubject.OnNext(sentimentResult);
        }

        var totalScore = sentimentDataList.Sum(data => data.Score);
        var averageScore = totalScore / sentimentDataList.Count;

        bool averageSentiment = averageScore > 0;

        // sort the sentimentDataList based on Score
        var sortedSentiments = sentimentDataList.OrderByDescending(data => data.Score);

        // print the most positive and most negative comments
        var mostPositiveComment = sortedSentiments.First();
        var mostNegativeComment = sortedSentiments.Last();

        var result = new
        {
            Sentiments = sentimentDataList,
            Summary = new
            {
                AverageSentiment = averageScore,
                MostPositiveComment = new
                {
                    mostPositiveComment.SentimentText,
                    mostPositiveComment.Sentiment,
                    mostPositiveComment.Score
                },
                MostNegativeComment = new
                {
                    mostNegativeComment.SentimentText,
                    mostNegativeComment.Sentiment,
                    mostNegativeComment.Score
                }
            }
        };

        return result;
    }
}

public class SentimentData
{
    [LoadColumn(0)]
    public float ItemID { get; set; }

    [LoadColumn(1)]
    public bool Sentiment { get; set; }

    [LoadColumn(2)]
    public string? SentimentText { get; set; }

    [LoadColumn(3)]
    public float Score { get; set; }
}

public class SentimentPrediction
{
    [ColumnName("PredictedLabel")]
    public bool Prediction { get; set; }
    public float Score { get; set; }
}

public class SentimentAnalysisResult
{
    public string? SentimentText { get; set; }
    public bool Sentiment { get; set; }
    public float Score { get; set; }
}
