// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QnAMakerBot.Dialogs;
using QnAMakerBot.Helpers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QnAMakerBot.Bots
{
    // This IBot implementation can run any type of Dialog. The use of type parameterization is to allows multiple different bots
    // to be run at different endpoints within the same project. This can be achieved by defining distinct Controller types
    // each with dependency on distinct IBot types, this way ASP Dependency Injection can glue everything together without ambiguity.
    // The ConversationState is used by the Dialog system. The UserState isn't, however, it might have been used in a Dialog implementation,
    // and the requirement is that all BotState objects are saved at the end of a turn.
    public class QnABot : DialogBot<QnADialog>
    {
        private readonly string welcomeMessage;
        private readonly bool _azureAdEnabled;
        private readonly string _connectionName;
        private readonly DialogSet _dialogSet;

        public QnABot(ConversationState conversationState, UserState userState, IQnAService qnaService, ILogger<QnABot> logger, IConfiguration configuration)
             : base(conversationState, userState, new QnADialog(qnaService, configuration), logger, configuration)
        {
            welcomeMessage = _configuration.GetValue<string>("WelcomeMessage");
            _azureAdEnabled = configuration["AzureAd:Enable"].ToLower() == "true" ? true : false;
            _connectionName = _configuration["AzureAd:ConnectionName"];
            var dialogState = _conversationState.CreateProperty<DialogState>("DialogState");
            _dialogSet = new DialogSet(dialogState);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                // Greet anyone that was not the target (recipient) of this message.
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageHelper.GetMessageActivity(welcomeMessage, turnContext.Activity.ChannelId), cancellationToken);
                    await AuthenticateUser(turnContext, cancellationToken);
                }
            }
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var text = string.Empty;
            if (!string.IsNullOrEmpty(turnContext.Activity.Text))
            {
                text = turnContext.Activity.Text.ToLowerInvariant();
            }

            if (text == "logout" && _azureAdEnabled)
            {
                // The bot adapter encapsulates the authentication processes.
                var botAdapter = (BotFrameworkAdapter)turnContext.Adapter;
                await botAdapter.SignOutUserAsync(turnContext, _connectionName, null, cancellationToken);
                await turnContext.SendActivityAsync(MessageHelper.GetMessageActivity(_configuration["AzureAd:LoggedOutMessage"], turnContext.Activity.ChannelId), cancellationToken);

                var dialogContext = await _dialogSet.CreateContextAsync(turnContext, cancellationToken);
                await dialogContext.CancelAllDialogsAsync();
            }
            else
            {
                if (await AuthenticateUser(turnContext, cancellationToken) == false)
                {
                    await base.OnMessageActivityAsync(turnContext, cancellationToken);
                }
            }

            // Save any state changes that might have occured during the turn.
            await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await _userState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        private async Task<bool> AuthenticateUser(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            bool waitingForLogin = false;
            // perform authorization check ... if already authorized, nothing happens, if not auth process is started
            if (_azureAdEnabled)
            {
                var botAdapter = (BotFrameworkAdapter)turnContext.Adapter;
                var token = await botAdapter.GetUserTokenAsync(turnContext, _connectionName, null, cancellationToken);

                if (token == null)
                {
                    var promptSettings = new OAuthPromptSettings
                    {
                        ConnectionName = _configuration["AzureAd:ConnectionName"],
                        Text = _configuration["AzureAd:LoginText"],
                        Title = _configuration["AzureAd:ButtonText"],
                        Timeout = 300000, // User has 5 minutes to login
                    };
                    var authPrompt = new OAuthPrompt(nameof(OAuthPrompt), promptSettings);
                    await authPrompt.Run(turnContext, _conversationState.CreateProperty<DialogState>("DialogState"), cancellationToken);
                    waitingForLogin = true;

                    // Save any state changes that might have occured during the turn.
                    await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
                    await _userState.SaveChangesAsync(turnContext, false, cancellationToken);
                }
            }

            return waitingForLogin;
        }

        protected override async Task OnTokenResponseEventAsync(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            await base.OnTokenResponseEventAsync(turnContext, cancellationToken);

            await CompleteSignIn(turnContext, cancellationToken);
        }

        protected override async Task OnUnrecognizedActivityTypeAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Type == "invoke")
            {
                var promptSettings = new OAuthPromptSettings
                {
                    ConnectionName = _configuration["AzureAd:ConnectionName"],
                    Text = _configuration["AzureAd:LoginText"],
                    Title = _configuration["AzureAd:ButtonText"],
                    Timeout = 300000, // User has 5 minutes to login
                };
                var authPrompt = new OAuthPrompt(nameof(OAuthPrompt), promptSettings);
                await authPrompt.Run(turnContext, _conversationState.CreateProperty<DialogState>("DialogState"), cancellationToken);
                await CompleteSignIn(turnContext, cancellationToken);
            }
            else
            {
                await base.OnUnrecognizedActivityTypeAsync(turnContext, cancellationToken);
            }
        }

        private async Task CompleteSignIn(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            // Get the token, and use it to check if the user is in the required group
            var botAdapter = (BotFrameworkAdapter)turnContext.Adapter;
            var token = await botAdapter.GetUserTokenAsync(turnContext, _connectionName, null, cancellationToken);
            if (token != null)
            {
                await turnContext.SendActivityAsync(MessageHelper.GetMessageActivity(_configuration["AzureAd:LoggedInMessage"], turnContext.Activity), cancellationToken);
            }

            // End the dialog
            var dialogContext = _dialogSet.CreateContextAsync(turnContext, cancellationToken).Result;
            await dialogContext.EndDialogAsync(true);
        }
    }
}
