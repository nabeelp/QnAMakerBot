// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace QnAMakerBot.Models
{
    public class QnALearningState : QnABotState
    {
        public List<QnAResult> QnAData { get; set; }
    }
}
