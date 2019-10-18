// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Builder.AI.QnA;
using QnAMakerBot.Models;
using System.Threading.Tasks;

namespace QnAMakerBot.Helpers
{
    public interface IQnAService
    {
        Task<QnAResult[]> QueryQnAServiceAsync(string query, QnABotState qnAcontext);
        Task TrainQnAServiceAsync(FeedbackRecords feedbackRecords);
    }
}
