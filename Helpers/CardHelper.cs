// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Schema;
using QnAMakerBot.Models;
using System.Collections.Generic;
using System.Linq;

namespace QnAMakerBot.Helpers
{
    public class CardHelper
    {
        /// <summary>
        /// Get Hero card
        /// </summary>
        /// <param name="cardSubtitle">Title of the card</param>
        /// <param name="prompts">List of suggested prompts</param>
        /// <returns>Message activity</returns>
        public static Activity GetHeroCard(string cardSubtitle, QnAPrompts[] prompts)
        {
            var buttons = new List<CardAction>();

            var sortedPrompts = prompts.OrderBy(r => r.DisplayOrder);
            foreach (var prompt in sortedPrompts)
            {
                buttons.Add(
                    new CardAction()
                    {
                        Value = prompt.DisplayText,
                        Type = ActionTypes.ImBack,
                        Title = prompt.DisplayText,
                    });
            }

            return CreateHeroCard(string.Empty, cardSubtitle, buttons);
        }

        /// <summary>
        /// Get Hero card
        /// </summary>
        /// <param name="suggestionsList">List of suggested questions</param>
        /// <param name="cardTitle">Title of the cards</param>
        /// <param name="cardNoMatchText">No match text</param>
        /// <returns></returns>
        public static Activity GetHeroCard(List<string> suggestionsList, string cardTitle, string cardNoMatchText)
        {
            var buttonList = new List<CardAction>();

            // Add all suggestions
            foreach (var suggestion in suggestionsList)
            {
                buttonList.Add(
                    new CardAction()
                    {
                        Value = suggestion,
                        Type = "imBack",
                        Title = suggestion,
                    });
            }

            // Add No match text
            buttonList.Add(
                new CardAction()
                {
                    Value = cardNoMatchText,
                    Type = "imBack",
                    Title = cardNoMatchText
                });

            return CreateHeroCard(cardTitle, string.Empty, buttonList);
        }

        private static Activity CreateHeroCard(string cardTitle, string cardSubtitle, List<CardAction> buttons)
        {
            var chatActivity = Activity.CreateMessageActivity();
            var plCard = new HeroCard()
            {
                Title = cardTitle,
                Subtitle = cardSubtitle,
                Buttons = buttons
            };

            var attachment = plCard.ToAttachment();

            chatActivity.Attachments.Add(attachment);

            return (Activity)chatActivity;
        }

    }
}
