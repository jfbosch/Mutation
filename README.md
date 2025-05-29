# Mutation

## Introduction
Mutation is a .NET multifaceted tool that boosts productivity and accessibility through configurable global hotkeys. It lets you toggle microphones, capture screens, run Optical Character Recognition (OCR), convert speech to text, and review transcripts with the ChatGPT-4 LLM—using Azure Vision Services, OpenAI, Deepgram, and other APIs under the hood.

## Features
### 1. Toggle Microphone Mute  
Press one hotkey to mute or unmute every enabled microphone system-wide—independent of which meeting app or input device you use.

### 2. Screen Capturing and OCR  
Mutation supports two OCR reading orders via Azure **Computer Vision** (part of Vision Services):

* **Natural layout** – reads top-to-bottom within each column, then left-to-right across columns. Best for newspapers, journals, brochures, or any multi-column PDF.  
* **Basic layout** – reads strictly left-to-right, top-to-bottom. Best for tables, spreadsheets, forms, invoices, or any row-oriented content.

Hotkeys:

* **ScreenshotHotKey** – instantly captures the full screen and then lets you draw a rectangle with a crosshair cursor (press **Esc** to cancel); the selected region goes to the clipboard.
* **OcrHotKey** – OCR the clipboard image with **Natural** layout.  
* **ScreenshotOcrHotKey** – take a screenshot and OCR it with **Natural** layout in one step.  
* **OcrLeftToRightTopToBottomHotKey** – OCR the clipboard image with **Basic** layout.  
* **ScreenshotLeftToRightTopToBottomOcrHotKey** – take a screenshot and OCR it with **Basic** layout in one step.

Azure Computer Vision greatly outperforms the Windows 10/11 built-in OCR engine.

### 3. Speech to Text Conversion  
Press one hotkey to start recording, press it again to stop and send the audio for transcription. Supported providers:

* OpenAI Whisper family (gpt-4o-transcribe, gpt-4o-mini-transcribe)  
* Deepgram nova-2 / nova-3  
* Any service exposing an OpenAI-compatible Whisper API

The transcribed text is copied to the clipboard and can optionally be injected into the active text field.

### 4. Transcript Review with ChatGPT  
Send any block of text to ChatGPT (via Azure OpenAI) for summarisation, proofreading, or rewriting, directly from a hotkey.

## Getting Started
Install the .NET 7 runtime (or newer) and run **Mutation.exe**. On first launch, the app writes *Mutation.json* and opens it in Notepad for you to configure.

## Configuration / Settings
All hotkeys are global and fully customisable. Below is a comprehensive example with every section and key.

```json
{
  "ToggleMicMuteHotKey": "Ctrl+Shift+M",
  "ScreenshotHotKey": "Ctrl+Shift+S",

  "AzureComputerVisionSettings": {
    "SubscriptionKey": "<your Azure key>",
    "Endpoint": "https://<region>.api.cognitive.microsoft.com/",
    "OcrHotKey": "Ctrl+Shift+O",
    "ScreenshotOcrHotKey": "Ctrl+Shift+Q",
    "OcrLeftToRightTopToBottomHotKey": "Ctrl+Shift+L",
    "ScreenshotLeftToRightTopToBottomOcrHotKey": "Ctrl+Shift+K",
    "SendHotKeyAfterOcrOperation": "Ctrl+Alt+C"
  },

  "SpeetchToTextSettings": {
    "StartStopTranscriptionHotKey": "Ctrl+Shift+T",
    "SendHotKeyAfterTranscriptionOperation": "Ctrl+Alt+V",
    "Providers": [
      {
        "Name": "OpenAI gpt-4o-transcribe",
        "Provider": "OpenAi",
        "ApiKey": "<your OpenAI key>",
        "BaseDomain": "https://api.openai.com/",
        "ModelId": "gpt-4o-transcribe"
      },
      {
        "Name": "Groq Whisper 3",
        "Provider": "OpenAi",
        "ApiKey": "<your Groq key>",
        "BaseDomain": "https://api.groq.com/openai/",
        "ModelId": "whisper-large-v3"
      },
      {
        "Name": "Deepgram Nova3",
        "Provider": "Deepgram",
        "ApiKey": "<your Deepgram key>",
        "BaseDomain": null,
        "ModelId": "nova-3"
      }
    ]
  },

  "LlmSettings": {
    "ApiKey": "<your Azure OpenAI key>",
    "Endpoint": "https://<resource>.openai.azure.com/",
    "ModelId": "gpt-4o-turbo"
  }
}
````

### Provisioning Azure Computer Vision

1. Sign in to the [Azure Portal](https://portal.azure.com).
2. **Create a resource** → search for **Computer Vision**.
3. Choose your subscription, resource group, region, name, and the free pricing tier.
4. After deployment, copy the **Key** and **Endpoint** into `AzureComputerVisionSettings` and restart Mutation.

### Provisioning Speech-to-Text Providers

* Follow each provider’s portal to create an account and API key.
* Paste the credentials into the relevant object under `SpeetchToTextSettings → Providers`.

### Enabling ChatGPT Review

Deploy `gpt-4o` or `gpt-4.1`, or other preferred model, and add the key and endpoint to `LlmSettings`. You pay only for actual **token** usage.

## Contribute

Pull requests are welcome—open an issue to discuss ideas first, then fork, commit, and PR.

## License

See *LICENCE* in the repository.


## Backstory.
So I got tired of having to learn the hotkeys of all the different online meeting applications that I use for toggling the microphone on and off mute. As a visually impaired computer user, finding the microphone icon visually and clicking on it is not really a viable option. I'm a very heavy AutoHotKey user, and I first tried to build a solution with that, but it was clunky. I then asked a buddy of mine if he has some experience with manipulating the microphone with C#. He didn't, but he quickly put together something in LINQPad to toggle the microphone using the audio switcher library. I then took that code and started a little WinForms application that had the microphone toggle functionality wired up to a global hotkey., and I called it Mutation. As in, I could mute the microphone at any time I wanted, no matter which application I was busy working in. This was incredibly useful, but I once had the situation where Microsoft Teams was using my second microphone and not the main one, and so when I thought I was muted with mutation, the second mic was still active and the person on the call heard while I was talking to someone locally. Luckily, it wasn't too embarrassing. I then updated mutation to list all the detected microphones and to mute and unmute them all on the toggle. In that way, I could be sure that when I wanted it muted, it was definitely muted across my system, across all the microphones. This capability became indispensable to me in my daily usage and meetings.

Being almost blind, I have the problem, like many others in the same situation, where I could not really read any screenshots or images containing text. And those come along more often than you realize in my kind of work. So, what I did was to provision myself a free Microsoft Computer Vision resource on my Azure subscription and wired up a hotkey that grabs an image from the clipboard, performs OCR on it, and puts the text back on the clipboard. Suddenly, Mutation became even more useful. This worked great for images that came our way over emails or instant messages, etc., but if I wanted to create my own screenshot of a portion of the screen, I still had to use a third-party application to put the screenshot on the clipboard. I decided, why can't mutation do that for me as well? So I extended it with the capability, again wired up to a hotkey, to take a screenshot of the entire application and then allow a rectangle selection with the mouse. At the end of the mouse drag, the image would be copied automatically onto the clipboard. I added a second hotkey that combined the screenshot and the OCR into an automated process. Now I could press a hotkey, select a rectangle on the screen, OCR was automatically performed and the text was placed on the clipboard. At which point I can just press another hotkey to read the contents of the clipboard with my screen reader.

Being extremely impressed with the OpenAI Whisper model's capability of speech-to-text while using the ChatGPT app on my iPhone, I wanted to start using it on my desktop as well. I tried using the OpenAI Whisper model on my local computer for dictation. I downloaded an application called Buzz that wrapped the model. Unfortunately, using the smaller models did not have very accurate transcription and using the larger models was unbelievably slow on my development workstation.
So I decided to wire up Mutation to record an MP3 when I press a hotkey, and then send that MP3 to the OpenAI Whisper API for transcription, and then to put the text back on the clipboard, at which point it's again available for my screen reader, or just to paste into a document. Typically, this is very fast for dictating a couple of sentences. It only takes one to three seconds to come back with the text.
In fact, I'm using mutation and whisper to dictate this entire backstory of mutation. This feature is quite the productivity booster. I find it saves me a lot of time, as for a lot of messages, even short messages, like on WhatsApp or Slack, it's much faster to speak them and then paste the resulting text than to type it out.

I don't think many people will use mutation, but I'm sure there will be a few that will find the kind of productivity boosting that it can give incredibly useful. and thus the open-source project was born.
For myself, it is absolutely indispensable, and I could not go a day without it anymore. I will add to it as I think of more tools to make my life easier.

Here's hoping it helps somebody else as well.

