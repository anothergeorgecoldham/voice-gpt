using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Azure;
using Azure.AI.OpenAI;
using static System.Environment;

class Program 
{
    // This example requires environment variables named "OPEN_AI_KEY" and "OPEN_AI_ENDPOINT"
    // Your endpoint should look like the following https://YOUR_OPEN_AI_RESOURCE_NAME.openai.azure.com/
    static string openAIKey = Environment.GetEnvironmentVariable("OPEN_AI_KEY");
    static string openAIEndpoint = Environment.GetEnvironmentVariable("OPEN_AI_ENDPOINT");

    // Enter the deployment name you chose when you deployed the model.
    static string engine = "text-davinci-002";

    // This example requires environment variables named "SPEECH_KEY" and "SPEECH_REGION"
    static string speechKey = Environment.GetEnvironmentVariable("SPEECH_KEY");
    static string speechRegion = Environment.GetEnvironmentVariable("SPEECH_REGION");

    // Prompts Azure OpenAI with a request and synthesizes the response.
    async static Task AskOpenAI(string prompt)
    {
        // Ask Azure OpenAI
        OpenAIClient client = new(new Uri(openAIEndpoint), new AzureKeyCredential(openAIKey));
        var completionsOptions = new CompletionsOptions()
        {
            Prompts = { prompt },
            MaxTokens = 100,
        };
        Response<Completions> completionsResponse = client.GetCompletions(engine, completionsOptions);
        string text = completionsResponse.Value.Choices[0].Text.Trim();
        Console.WriteLine($"Azure OpenAI response: {text}");

        var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
        // The language of the voice that speaks.
        speechConfig.SpeechSynthesisVoiceName = "en-GB-SoniaNeural"; 
        var audioOutputConfig = AudioConfig.FromDefaultSpeakerOutput();

        using (var speechSynthesizer = new SpeechSynthesizer(speechConfig, audioOutputConfig))
        {
            var speechSynthesisResult = await speechSynthesizer.SpeakTextAsync(text).ConfigureAwait(true);

            if (speechSynthesisResult.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                Console.WriteLine($"Speech synthesized to speaker for text: [{text}]");
            }
            else if (speechSynthesisResult.Reason == ResultReason.Canceled)
            {
                var cancellationDetails = SpeechSynthesisCancellationDetails.FromResult(speechSynthesisResult);
                Console.WriteLine($"Speech synthesis canceled: {cancellationDetails.Reason}");

                if (cancellationDetails.Reason == CancellationReason.Error)
                {
                    Console.WriteLine($"Error details: {cancellationDetails.ErrorDetails}");
                }
            }
        }
    }

    // Continuously listens for speech input to recognize and send as text to Azure OpenAI
    async static Task ChatWithOpenAI()
    {
        // Should be the locale for the speaker's language.
        var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);        
        speechConfig.SpeechRecognitionLanguage = "en-US";

        using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        using var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
        var conversationEnded = false;
        var indigoAttention = false;

        while(!conversationEnded)
        {
            Console.WriteLine("Azure OpenAI is listening. Say 'Stop' or press Ctrl-Z to end the conversation.");

            // Get audio from the microphone and then send it to the TTS service.
            var speechRecognitionResult = await speechRecognizer.RecognizeOnceAsync();           

            switch (speechRecognitionResult.Reason)
            {
                case ResultReason.RecognizedSpeech:
                    if (speechRecognitionResult.Text == "Stop.")
                    {
                        Console.WriteLine($"Recognized speech: {speechRecognitionResult.Text}");
                        Console.WriteLine("Conversation ended.");
                        conversationEnded = true;
                    }
                    else if (speechRecognitionResult.Text == "Hey, Indigo.")
                    {
                        Console.WriteLine("Ok, I'm listening.");
                        indigoAttention = true;
                    }
                    else if (speechRecognitionResult.Text == "Thanks Indigo.")
                    {
                        Console.WriteLine("You're Welcome");
                        string text = "You're Welcome";
                        //await synthesizer.SpeakTextAsync(text)
                        indigoAttention = false;
                        conversationEnded = true;
                    }
                    else
                    {
                        if(indigoAttention == true){ 
                        Console.WriteLine($"Recognized speech: {speechRecognitionResult.Text}");
                        await AskOpenAI(speechRecognitionResult.Text).ConfigureAwait(true);
                        }
                    }
                    break;
                case ResultReason.NoMatch:
                    Console.WriteLine($"Recognized speech: {speechRecognitionResult.Text}");
                    Console.WriteLine($"No speech could be recognized: ");
                    indigoAttention = false;
                    break;
                case ResultReason.Canceled:
                    var cancellationDetails = CancellationDetails.FromResult(speechRecognitionResult);
                    Console.WriteLine($"Speech Recognition canceled: {cancellationDetails.Reason}");
                    if (cancellationDetails.Reason == CancellationReason.Error)
                    {
                        Console.WriteLine($"Error details={cancellationDetails.ErrorDetails}");
                    }
                    break;
            }
        }
    }

    async static Task Main(string[] args)
    {
        try
        {
            await ChatWithOpenAI().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}