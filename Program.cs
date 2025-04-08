using System;
using System.Diagnostics;
using System.IO;

namespace youtubeTranscriber
{
    class Program
    {
        /// <summary>
        /// Entry point for the application.
        /// Usage: youtubeTranscriber <YouTube Video URL>
        /// </summary>
        /// <param name="args">Command line arguments (expects a YouTube video URL)</param>
        static void Main(string[] args)
        {
            // Check if a YouTube URL was provided as an argument.
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: youtubeTranscriber <YouTube Video URL>");
                return;
            }

            // Retrieve the video URL from the command-line arguments.
            string videoUrl = args[0];
            Console.WriteLine("Downloading audio from: " + videoUrl);

            // Define the output template that yt-dlp will use and the expected output file.
            string outputTemplate = "audio.%(ext)s";  // yt-dlp will replace %(ext)s with the appropriate extension.
            string audioFile = "audio.mp3";           // We are extracting audio in mp3 format.

            // Step 1: Download the audio using yt-dlp.
            if (!DownloadAudio(videoUrl, outputTemplate))
            {
                Console.WriteLine("Audio download failed.");
                return;
            }

            // Step 2: Check if the file exists, then transcribe using a Python script.
            if (File.Exists(audioFile))
            {
                Console.WriteLine("Audio downloaded successfully. Transcribing...");

                // Call the Python script to get the transcription.
                string transcription = TranscribeAudio(audioFile);
                Console.WriteLine("Transcription:");
                Console.WriteLine(transcription);
            }
            else
            {
                Console.WriteLine("Audio file not found. Something went wrong during the download process.");
            }
        }

        /// <summary>
        /// Downloads the audio stream from the YouTube video using yt-dlp.
        /// </summary>
        /// <param name="videoUrl">URL of the YouTube video</param>
        /// <param name="outputTemplate">Output template for yt-dlp (e.g., "audio.%(ext)s")</param>
        /// <returns>True if the process executes; errors are logged otherwise</returns>
        static bool DownloadAudio(string videoUrl, string outputTemplate)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"--extract-audio --audio-format mp3 -o \"{outputTemplate}\" {videoUrl}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    Console.WriteLine("yt-dlp Output:");
                    Console.WriteLine(output);

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine("yt-dlp Errors:");
                        Console.WriteLine(error);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception during audio download: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Calls a Python script to transcribe the downloaded audio file.
        /// Assumes a Python script named 'whisper_transcribe.py' is available in the working directory.
        /// </summary>
        /// <param name="audioFilePath">Path to the downloaded audio file (e.g., audio.mp3)</param>
        /// <returns>The transcription string from the Python script</returns>
        static string TranscribeAudio(string audioFilePath)
        {
            try
            {
                // The command here calls "whisper" directly. Make sure that the whisper CLI is available in your PATH.
                // You may adjust the arguments (e.g., --model small) as necessary.
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "whisper", // If not available via PATH, use the full path, e.g., "/opt/homebrew/bin/whisper"
                    Arguments = $"{audioFilePath} --model small",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    // Capture standard output and error.
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        Console.WriteLine("Whisper Errors:");
                        Console.WriteLine(error);
                    }
                    return output;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception during transcription: " + ex.Message);
                return string.Empty;
            }
        }
    }
}