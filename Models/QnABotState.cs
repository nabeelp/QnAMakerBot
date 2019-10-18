// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace QnAMakerBot.Models
{
    public class QnABotState
    {
        public int PreviousQnaId { get; set; }

        public string PreviousUserQuery { get; set; }
        public QnAPrompts[] PreviousPrompts { get; set; }
    }
}
