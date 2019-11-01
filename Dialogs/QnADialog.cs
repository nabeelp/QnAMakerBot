// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Designer.Dialogs;
using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using QnAMakerBot.Helpers;
using QnAMakerBot.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QnAMakerBot.Dialogs
{
    public class QnADialog : FunctionDialogBase
    {
        private readonly IQnAService _qnaService;
        private readonly IConfiguration _configuration;
        private readonly string learningCardTitle = "Did you mean:";
        private readonly string learningCardNoMatchText = "None of the above.";
        private readonly string learningCardNoMatchResponse = "Thanks for the feedback.";
        private readonly string noQnAResults = "No match found.";
        private readonly float highConfidenceThreshold = 0.8f;
        private readonly float lowConfidenceThreshold = 0.4f;
        private readonly int maxQnaResults = 3;

        public QnADialog(IQnAService qnaService, IConfiguration configuration)
            : base(nameof(QnADialog))
        {
            _qnaService = qnaService;
            _configuration = configuration;

            // populate variables from config
            float.TryParse(_configuration["QnAHighConfidenceThreshold"], out highConfidenceThreshold);
            float.TryParse(_configuration["QnALowConfidenceThreshold"], out lowConfidenceThreshold);
            int.TryParse(_configuration["QnATopResults"], out maxQnaResults);
            noQnAResults = _configuration["QnANoMatchFoundMessage"];
            learningCardTitle = _configuration["QnALearningCard:Title"];
            learningCardNoMatchText = _configuration["QnALearningCard:NoMatchText"];
            learningCardNoMatchResponse = _configuration["QnALearningCard:NoMatchResponse"];
        }

        protected override async Task<(object newState, IEnumerable<Activity> output, object result)> ProcessAsync(object oldState, Activity inputActivity)
        {
            // check the previous state to determine if we are asking a new question or learning
            if (oldState == null || (oldState != null && oldState.GetType() == typeof(QnABotState)))
            {
                return await QueryQnA(oldState as QnABotState, inputActivity);
            }
            else
            {
                // try and learn from the response ... if nothing to learn, then user probably entered an unexpected response, so run the normal QnA
                var learnResult = await TrainQnA(oldState as QnALearningState, inputActivity);
                if (learnResult.output != null && ((Activity[])learnResult.output)[0] != null)
                {
                    return learnResult;
                }
                else
                {
                    oldState = null;
                    return await QueryQnA(oldState as QnABotState, inputActivity);
                }
            }
        }

        private async Task<(object newState, IEnumerable<Activity> output, object result)> QueryQnA(QnABotState oldState, Activity inputActivity)
        {
            Activity outputActivity = null;
            QnABotState newState = null;

            // new question
            var query = inputActivity.Text;
            var qnaResult = await _qnaService.QueryQnAServiceAsync(query, (QnABotState)oldState);

            // get answers that are above the two thresholds
            var highConfidenceResults = qnaResult.Where(answer => answer.Score > (highConfidenceThreshold * 100)).ToList();
            var lowConfidenceResults = qnaResult.Where(answer => answer.Score > (lowConfidenceThreshold * 100)).ToList();

            // however, if this query was done after a multi-turn, then we just take the first result, and thus force this first result to be displayed
            if (oldState != null && oldState.PreviousPrompts != null)
            {
                var matchingPrompt = oldState.PreviousPrompts.Where(x => x.DisplayText.ToLower() == query.ToLower());
                if (matchingPrompt.Count() > 0)
                {
                    highConfidenceResults = new List<QnAResult>
                    {
                        qnaResult.First()
                    };
                }
            }

            if (highConfidenceResults.Count >= 1)
            {
                // take the first high confidence match found
                var qnaAnswer = highConfidenceResults[0].Answer;
                var prompts = highConfidenceResults[0].Context?.Prompts;
                var qnaId = highConfidenceResults[0].Id;

                // handle prompts, if there are any, as part of the multi-turn structures
                if (prompts == null || prompts.Length < 1)
                {
                    outputActivity = MessageHelper.GetMessageActivity(qnaAnswer, inputActivity);
                }
                else
                {
                    // Set bot state only if prompts are found in QnA result
                    newState = new QnABotState
                    {
                        PreviousQnaId = qnaId,
                        PreviousUserQuery = query,
                        PreviousPrompts = prompts
                    };

                    outputActivity = CardHelper.GetHeroCard(qnaAnswer, prompts);
                }
            }
            else if (highConfidenceResults.Count == 0 && lowConfidenceResults.Count == 0)
            {
                // no matches found
                outputActivity = MessageHelper.GetMessageActivity(noQnAResults, inputActivity);
            }
            else
            {
                // more than 1 match found, so we need to present an opportunity to learn from the user
                var suggestedQuestions = new List<string>();
                foreach (var qna in lowConfidenceResults)
                {
                    suggestedQuestions.Add(qna.Questions[0]);
                }

                // Get hero card activity
                outputActivity = CardHelper.GetHeroCard(suggestedQuestions, learningCardTitle, learningCardNoMatchText);
                newState = new QnALearningState
                {
                    PreviousQnaId = 0,
                    PreviousUserQuery = inputActivity.Text,
                    QnAData = lowConfidenceResults
                };
            }

            // return the result
            return (newState, new Activity[] { outputActivity }, null);
        }

        private async Task<(object newState, IEnumerable<Activity> output, object result)> TrainQnA(QnALearningState oldState, Activity inputActivity)
        {
            Activity outputActivity = null;
            QnABotState newState = null;

            var trainResponses = oldState.QnAData;
            var currentQuery = oldState.PreviousUserQuery;

            var reply = inputActivity.Text;

            if (trainResponses.Count >= 1)
            {
                // find the question that matches the one selected from the list of lower confidence questions
                var qnaResult = trainResponses.Where(kvp => kvp.Questions[0] == reply).FirstOrDefault();

                if (qnaResult != null)
                {
                    // one of the lower confidence questions was selected, so let's ensure that is in the state
                    newState = new QnABotState
                    {
                        PreviousQnaId = qnaResult.Id,
                        PreviousUserQuery = reply
                    };

                    var records = new FeedbackRecord[]
                    {
                        new FeedbackRecord
                        {
                            UserId = inputActivity.Id,
                            UserQuestion = currentQuery,
                            QnaId = qnaResult.Id,
                        }
                    };

                    var feedbackRecords = new FeedbackRecords { Records = records };

                    // Call Active Learning Train API
                    await _qnaService.TrainQnAServiceAsync(feedbackRecords);

                    // generate a thank you message
                    outputActivity = MessageHelper.GetMessageActivity(learningCardNoMatchResponse, inputActivity);

                    // send the selected question to QnA
                    var newQueryResult = await QueryQnA(newState, inputActivity);

                    return (newState, new Activity[] { outputActivity, newQueryResult.output.First() }, null);
                }
                else if (reply.Equals(learningCardNoMatchText))
                {
                    outputActivity = MessageHelper.GetMessageActivity(noQnAResults, inputActivity);
                }
            }

            // return the result
            return (newState, new Activity[] { outputActivity }, null);
        }
    }
}
