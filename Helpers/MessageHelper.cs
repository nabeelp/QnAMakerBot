// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MarkdownDeep;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using System;

namespace QnAMakerBot.Helpers
{
    public class MessageHelper
    {
        /// <summary>
        /// Get the supplied text as a suitable message for the current channel
        /// </summary>
        /// <param name="message">String message to be sent to the bhot user</param>
        /// <param name="activity">The current activity in the dialog</param>
        /// <returns>Message formatted for the current channel</returns>
        public static Activity GetMessageActivity(string message, Activity activity)
        {
            return GetMessageActivity(message, activity.ChannelId);
        }

        /// <summary>
        /// Get the supplied text as a suitable message for the current channel
        /// </summary>
        /// <param name="message">String message to be sent to the bhot user</param>
        /// <param name="activity">The current activity in the dialog</param>
        /// <returns>Message formatted for the current channel</returns>
        public static Activity GetMessageActivity(string message, string channelId)
        {
            // if we are in the msteams channel, we have to convert any markdown in the qnaAnswer into HTML
            // otherwise, do placeholder replacement
            switch (channelId)
            {
                case "msteams":
                    var markdown = new Markdown();
                    message = markdown.Transform(message);
                    break;
                default:
                    message = message.Replace("\\r\\n", Environment.NewLine).Replace("\\n", Environment.NewLine);
                    break;
            }

            return MessageFactory.Text(message);
        }
    }
}
