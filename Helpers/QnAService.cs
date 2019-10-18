﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using QnAMakerBot.Models;
using System.Net.Http;
using System.Threading.Tasks;

namespace QnAMakerBot.Helpers
{
    public class QnAService : IQnAService
    {
        private readonly HttpClient _httpClient;
        private readonly QnAMakerEndpoint _endpoint;
        private readonly QnAMakerOptions _options;

        public QnAService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            (_options, _endpoint) = InitQnAService(configuration);
        }

        public async Task<QnAResult[]> QueryQnAServiceAsync(string query, QnABotState qnAcontext)
        {
            var requestUrl = $"{_endpoint.Host}/knowledgebases/{_endpoint.KnowledgeBaseId}/generateanswer";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            var jsonRequest = JsonConvert.SerializeObject(
                new
                {
                    question = query,
                    top = _options.Top,
                    context = qnAcontext,
                    strictFilters = _options.StrictFilters,
                    metadataBoost = _options.MetadataBoost,
                    scoreThreshold = _options.ScoreThreshold,
                }, Formatting.None);

            request.Headers.Add("Authorization", $"EndpointKey {_endpoint.EndpointKey}");
            request.Content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();


            var contentString = await response.Content.ReadAsStringAsync();

            var result = JsonConvert.DeserializeObject<QnAResultList>(contentString);

            return result.Answers;
        }

        public async Task TrainQnAServiceAsync(FeedbackRecords feedbackRecords)
        {
            QnAMaker trainer = new QnAMaker(_endpoint);
            await trainer.CallTrainAsync(feedbackRecords);
        }

        private static (QnAMakerOptions options, QnAMakerEndpoint endpoint) InitQnAService(IConfiguration configuration)
        {
            int topResults = 1;
            int.TryParse(configuration["QnATopResults"], out topResults);

            float scoreThreshold = 0.7f;
            float.TryParse(configuration["QnAScoreThreshold"], out scoreThreshold);

            var options = new QnAMakerOptions
            {
                Top = topResults,
                ScoreThreshold = scoreThreshold
            };

            var hostname = configuration["QnAEndpointHostName"];
            if (!hostname.StartsWith("https://"))
            {
                hostname = string.Concat("https://", hostname);
            }

            if (!hostname.EndsWith("/qnamaker"))
            {
                hostname = string.Concat(hostname, "/qnamaker");
            }

            var endpoint = new QnAMakerEndpoint
            {
                KnowledgeBaseId = configuration["QnAKnowledgebaseId"],
                EndpointKey = configuration["QnAAuthKey"],
                Host = hostname
            };

            return (options, endpoint);
        }
    }
}
