# QnAMakerBot
C# Chatbot to be used with QnA Maker, with additional capabilities like support for multi-prompt, user authentication, and explicit active learning

**Origins**: The bulk of the source for this project originated from the [QnAMaker-Prompting solution in the BotBuilder-Samples repo](https://github.com/microsoft/BotBuilder-Samples/tree/master/experimental/qnamaker-prompting/csharp_dotnetcore)

## Changes made
- Added support for AAD-based authentication of users. This will allow for the Bot to be restricted only to those users who are authorised as per the app registration config in Azure Active Directory.  To effect this you will need a second app registration, enabling control of user access independently of the Bot's internal authentication mechanism.
- Implemented [explicit Active Learning](https://docs.microsoft.com/en-us/azure/cognitive-services/qnamaker/how-to/improve-knowledge-base#how-you-give-explicit-feedback-with-the-train-api), which provides the user with an option to clarify his/her question when the results from QnAMaker are not of a high confidence.  The user's selection is then used to add a suggestion to the QnAMaker knowledgebase.

## Testing locally
- After cloning the repo, update [appsettings.json](appsettings.json) with your values.  
- If AzureAD auth is required, see the below for more info on setting this up
- Run the project (press `F5` key).

## Setting up Azure AD Authentication
There are a few steps involved in preparing your Bot to support Azure AD authentication:

- Create a new Azure AD app registration, as per https://docs.microsoft.com/en-us/azure/bot-service/bot-builder-authentication?view=azure-bot-service-4.0&tabs=csharp%2Cbot-oauth#create-and-register-an-azure-ad-application
- Create an AAD v2 OAuth Connection to your bot, as per https://docs.microsoft.com/en-us/azure/bot-service/bot-builder-authentication?view=azure-bot-service-4.0&tabs=csharp%2Cbot-oauth#azure-ad-v2
- Update the `AzureAD` section in [appsettings.json](appsettings.json) as follows:
  - Set _Enable_ to `true`
  - Set _ConnectionName_ to the value entered when creating the OAuth connection in your Bot's Settings page
- Run the project and test in the Bot Framework Emulator.  If all is configured correctly, you should be presented with an OAuth card after the welcome message

