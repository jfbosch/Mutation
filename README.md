# Mutation

## Introduction
The Mutation project is a simple .Net multifaceted tool designed to enhance productivity and accessibility for users via configurable, global hotkeys. Leveraging various technologies and cloud services, the application offers several features, including microphone toggle, screen capture, Optical Character Recognition (OCR), Speech to Text conversion, and transcript review with the ChatGPT 4 LLM.

## Features
### Toggle Microphone Mute
#### Hotkey that allows the user to mute/unmute all the enabled microphones system wide. Thus, no matter what communications application you are using for your online meetings, and no matter which of your microphones you are using with the various applications, it is very easy to mute and unmute the microphone with a global hotkey.

### Screen Capturing and OCR
#### Hotkey that allows user to draw a selection rectangle of the region on the screen that is of interest, and copy the resulting screenshot it to the clipboard as an image that can then be pasted into any compatible application.
#### Hotkey to perform OCR on the image on the clipboard using Microsoft Azure Computer Vision, and puts the extracted text back on to the clipboard.
#### hotkey that combines the above two to allow the user to select a region of the screen followed by an immediate OCR process using Microsoft Azure Computer Vision and placing the extracted text back on the clipboard.
Azure Computer Vision seems far superior at OCR than the built-in Windows 10 and 11 OCR engine.

### Speech to Text Conversion using OpenAI Whisper API
#### Hotkey to start recording the microphone, and when the same hotkey is pressed a second time, converting the speech to text that is placed on the clipboard.

## Getting Started
You need at least .NET 7 runtime installed, and then you can just run Mutation.exe.

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


#### Review transcript with ChatGPT API

If you want to use the LLM review capabilities, you will need to provision an Azure OpenAI service on the Azure portal.
Before you can do that, you have to apply. Here is the application form.
https://customervoice.microsoft.com/Pages/ResponsePage.aspx?id=v4j5cvGGr0GRqy180BHbR7en2Ais5pxKtso_Pz4b1_xUOFA5Qk1UWDRBMjg0WFhPMkIzTzhKQ1dWNyQlQCN0PWcu

Once you have been approved, you can join the GPT 4 waiting list as well.
https://customervoice.microsoft.com/Pages/ResponsePage.aspx?id=v4j5cvGGr0GRqy180BHbR7en2Ais5pxKtso_Pz4b1_xURjE4QlhVUERGQ1NXOTlNT0w1NldTWjJCMSQlQCN0PWcu

Note that you will require an existing Azure subscription ID during the application process. 

Once you have been approved in the Azure portal, which is at portal.azure.com, you can provision a new resource of the type Azure OpenAI, and then you can deploy any of the models that you want to use. Typically, that would be chat-gpt 3. 5 turbo and gpt 4. You will require the API key as well as the name that you named the Azure OpenAI resource to configure the feature in Mutation.json 
under the section called LlmSettings. (First run Mutation.exe to make sure the relevant settings in Mutation.json is created.)

When you deploy a new model, use the exact same deployment name as the model name. I.e. copy and paste it.

Note that there is no cost to just deploy the models. You will be charged for actual topen usage. Do familiarize yourself with the Azure OpenAI API pricing to avoid rude surprises. Specifically, GPT-4 starts getting very expensive very quickly. 



Lastly, simply run the application, and you'll have access to all the features accessible via hotkeys. You can then use these hotkeys to perform the actions described in the Features section.

## Contribute
Contributions to the project are welcome. Feel free to engage in discussion. Then, if your ideas are in line with the project’s objectives, fork the project, make your changes, and submit a pull request.

## License
Please refer to the license file in the repository.

## Backstory.
So I got tired of having to learn the hotkeys of all the different online meeting applications that I use for toggling the microphone on and off mute. As a visually impaired computer user, finding the microphone icon visually and clicking on it is not really a viable option. I'm a very heavy AutoHotKey user, and I first tried to build a solution with that, but it was clunky. I then asked a buddy of mine if he has some experience with manipulating the microphone with C#. He didn't, but he quickly put together something in LINQPad to toggle the microphone using the audio switcher library. I then took that code and started a little WinForms application that had the microphone toggle functionality wired up to a global hotkey., and I called it Mutation. As in, I could mute the microphone at any time I wanted, no matter which application I was busy working in. This was incredibly useful, but I once had the situation where Microsoft Teams was using my second microphone and not the main one, and so when I thought I was muted with mutation, the second mic was still active and the person on the call heard while I was talking to someone locally. Luckily, it wasn't too embarrassing. I then updated mutation to list all the detected microphones and to mute and unmute them all on the toggle. In that way, I could be sure that when I wanted it muted, it was definitely muted across my system, across all the microphones. This capability became indispensable to me in my daily usage and meetings.

Being almost blind, I have the problem, like many others in the same situation, where I could not really read any screenshots or images containing text. And those come along more often than you realize in my kind of work. So, what I did was to provision myself a free Microsoft Computer Vision resource on my Azure subscription and wired up a hotkey that grabs an image from the clipboard, performs OCR on it, and puts the text back on the clipboard. Suddenly, Mutation became even more useful. This worked great for images that came our way over emails or instant messages, etc., but if I wanted to create my own screenshot of a portion of the screen, I still had to use a third-party application to put the screenshot on the clipboard. I decided, why can't mutation do that for me as well? So I extended it with the capability, again wired up to a hotkey, to take a screenshot of the entire application and then allow a rectangle selection with the mouse. At the end of the mouse drag, the image would be copied automatically onto the clipboard. I added a second hotkey that combined the screenshot and the OCR into an automated process. Now I could press a hotkey, select a rectangle on the screen, OCR was automatically performed and the text was placed on the clipboard. At which point I can just press another hotkey to read the contents of the clipboard with my screen reader.

Being extremely impressed with the OpenAI Whisper model's capability of speech-to-text while using the ChatGPT app on my iPhone, I wanted to start using it on my desktop as well. I tried using the OpenAI Whisper model on my local computer for dictation. I downloaded an application called Buzz that wrapped the model. Unfortunately, using the smaller models did not have very accurate transcription and using the larger models was unbelievably slow on my development workstation.
So I decided to wire up Mutation to record an MP3 when I press a hotkey, and then send that MP3 to the OpenAI Whisper API for transcription, and then to put the text back on the clipboard, at which point it's again available for my screen reader, or just to paste into a document. Typically, this is very fast for dictating a couple of sentences. It only takes one to three seconds to come back with the text.
In fact, I'm using mutation and whisper to dictate this entire backstory of mutation. This feature is quite the productivity booster. I find it saves me a lot of time, as for a lot of messages, even short messages, like on WhatsApp or Slack, it's much faster to speak them and then paste the resulting text than to type it out.

I don't think many people will use mutation, but I'm sure there will be a few that will find the kind of productivity boosting that it can give incredibly useful. and thus the open-source project was born.
For myself, it is absolutely indispensable, and I could not go a day without it anymore. I will add to it as I think of more tools to make my life easier.

Here's hoping it helps somebody else as well.

