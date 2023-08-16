# Mutation

## Introduction
The Mutation project is a simple .Net multifaceted tool designed to enhance productivity and accessibility for users via configurable, global hotkeys. Leveraging various technologies and cloud services, the application offers several features, including screen capturing, Optical Character Recognition (OCR), Speech to Text conversion, and microphone control functionalities.

## Features
### Toggle Microphone Mute
#### Hotkey that allows the user to mute/unmute all the enabled microphones system wide. Thus, no matter what communications application you are using for your online meetings, and no matter which of your microphones you are using with the various applications, it is very easy to mute and unmute the microphone with a global hotkey.

### Screen Capturing and OCR
#### Hotkey that allows user to draw a selection rectangle of the region on the screen that is of interest, and copy the resulting screenshot it to the clipboard as an image that can then be pasted into any compatible application.
#### Hotkey to perform OCR on the image on the clipboard using Microsoft Azure Cognitive Services, and puts the extracted text back on to the clipboard.
#### hotkey that combines the above two to allow the user to select a region of the screen followed by an immediate OCR process using Microsoft Azure Cognitive Services and placing the extracted text back on the clipboard.

### Speech to Text Conversion using OpenAI Whisper API
#### Hotkey to start recording the microphone, and when the same hotkey is pressed a second time, converting the speech to text that is placed on the clipboard.

## Getting Started
You need at least .NET 6 installed, and then you can just run Mutation.exe.

### Configuration / Settings
All settings are stored in a file called Mutation.json. The first time you run Mutation.exe, it will create the JSON file and will open it in notepad. At that point, you can modify the config values, save it, and then restart mutation.
All hotkeys are global to the Windows desktop and are configurable in Mutation.json.

### Prerequisites
#### OCR
If you want to use the OCR functionality, you will need a Microsoft Azure subscription in which a Cognitive Services computer vision resource has been provisioned.
The free tier is quite sufficient for daily use by a single person.
#### Steps to provision the computer vision service
- This assumes you already have an Azure subscription (you can create this for free).
- In your browser, navigate to https://portal.azure.com
- Create a New Resource: Click "+ Create a resource," then search for and select "Computer Vision."
- Configure the Resource: On the "Create" page, enter the following details:
-- Subscription: Choose an available Azure subscription.
-- Resource Group: Select or create the group to contain the Azure AI services resource.
-- Region: Pick the location of the service instance. Note: Location may affect latency but not availability.
-- Name: Input a unique name for the new Computer Vision resource.
-- Pricing Tier: Select the free tier.
- Deploy: Review the information and click "Create."
- Access Keys & Endpoint: After deployment, find the keys and endpoint on the "Keys and Endpoint" page.
-- Edit Mutation.json, copy the key into the AzureComputerVisionSettings, SubscriptionKey value
-- Copy the Endpoint URL into the AzureComputerVisionSettings, Endpoint value.
- Save the JSON file and restart Mutation.

#### Speech to text
Whisper is an incredibly capable speech-to-text model developed by OpenAI. This application uses the OpenAI API to allow you to transcribe your voice at random in any application into text onto the clipboard with the press of a hotkey. The resulting text can then be pasted into the application of your choice. Whisper also supports many different languages. Check out the OpenAI site for more information.

If you want to use the speech-to-text functionality, you will need to create an OpenAI API account, add a credit card, configure a budget, generate API keys for the Whisper API, and configure the key in Mutation.json under OpenAiSettings, ApiKey.
Unfortunately, this only has a limited free credit, but in my experience it is fairly cheap even with quite aggressive daily use.
https://platform.openai.com/overview
Currently, in Mutation.json, the OpenAiSettings.Endpoint value is not used and can be left unpopulated. For now, the application will use the default Whisper endpoint.


Lastly, simply run the application, and you'll have access to all the features accessible via hotkeys. You can then use these hotkeys to perform the actions described in the Features section.

## Contribute
Contributions to the project are welcome. Feel free to engage in discussion. Then, if your ideas are in line with the project’s objectives, fork the project, make your changes, and submit a pull request.

## License
Please refer to the license file in the repository.
