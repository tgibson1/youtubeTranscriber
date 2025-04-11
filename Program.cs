using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;  // Install Newtonsoft.Json via NuGet.

namespace youtubeTranscriber
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: youtubeTranscriber <YouTube Video or Channel URL>");
                return;
            }
            
            string inputUrl = args[0];

            // Check for channel URLs including the new "@" handle format.
            if (inputUrl.Contains("youtube.com/channel") ||
                inputUrl.Contains("youtube.com/c/") ||
                inputUrl.Contains("youtube.com/@"))
            {
                ProcessChannel(inputUrl);
            }
            else
            {
                // Process single video using a generic folder name.
                ProcessVideo(inputUrl, "SingleVideoFolder");
            }
        }

        /// <summary>
        /// Processes a single video URL:
        /// Downloads audio, transcribes, and saves in the designated folder.
        /// </summary>
        static void ProcessVideo(string videoUrl, string folderPath)
        {
            Console.WriteLine("Processing video: " + videoUrl);
            Directory.CreateDirectory(folderPath);

            // Adjust output template to use folderPath.
            string outputTemplate = Path.Combine(folderPath, "audio.%(ext)s");
            string audioFile = Path.Combine(folderPath, "audio.mp3");

            if (!DownloadAudio(videoUrl, outputTemplate))
            {
                Console.WriteLine("Audio download failed for: " + videoUrl);
                return;
            }
            if (File.Exists(audioFile))
            {
                Console.WriteLine("Audio downloaded successfully. Transcribing...");
                string transcription = TranscribeAudio(audioFile);
                // Save transcription to a text file.
                File.WriteAllText(Path.Combine(folderPath, "transcription.txt"), transcription);
                Console.WriteLine("Transcription saved for: " + videoUrl);
            }
            else
            {
                Console.WriteLine("Audio file not found for: " + videoUrl);
            }
        }

        /// <summary>
        /// Processes an entire channel by fetching video metadata, then downloading and transcribing each video.
        /// </summary>
        static void ProcessChannel(string channelUrl)
        {
            Console.WriteLine("Processing channel: " + channelUrl);
            // Use yt-dlp to dump JSON info for all videos on the channel.
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "yt-dlp",
                Arguments = $"--dump-json {channelUrl}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            List<VideoMetadata> videos = new List<VideoMetadata>();
            using (Process process = Process.Start(psi))
            {
                string line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    // Each line is a JSON object for one video.
                    try
                    {
                        var metadata = JsonConvert.DeserializeObject<VideoMetadata>(line);
                        videos.Add(metadata);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("JSON parse error: " + ex.Message);
                    }
                }
                process.WaitForExit();
            }

            Console.WriteLine($"Found {videos.Count} videos.");

            // Loop through each video and process.
            foreach (var video in videos)
            {
                // Format upload_date from yyyyMMdd to yyyy-MM-dd.
                string formattedDate = FormatDate(video.upload_date);
                // Sanitize title to remove invalid file characters.
                string sanitizedTitle = SanitizeTitle(video.title);
                // Use the channel name for directory naming.
                string channelName = "CharlesTyler"; 
                string folderName = Path.Combine(channelName, $"{formattedDate}_{sanitizedTitle}");

                // Build the full YouTube video URL using the video ID.
                string videoUrl = "https://www.youtube.com/watch?v=" + video.id;
                ProcessVideo(videoUrl, folderName);
            }
        }

        /// <summary>
        /// Downloads the audio stream from the YouTube video using yt-dlp.
        /// </summary>
        static bool DownloadAudio(string videoUrl, string outputTemplate)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    // Added "--cookies-from-browser chrome" to enable age-restricted downloads.
                    Arguments = $"--extract-audio --audio-format mp3 --cookies-from-browser chrome -o \"{outputTemplate}\" {videoUrl}",
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

                    Console.WriteLine("yt-dlp Output:\n" + output);
                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine("yt-dlp Errors:\n" + error);
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
        /// Calls the Whisper transcription tool to transcribe the downloaded audio.
        /// Note: The audio file path is now quoted to properly handle spaces.
        /// </summary>
        static string TranscribeAudio(string audioFilePath)
        {
            try
            {
                // Quote the audioFilePath so that spaces in folder names do not break the command.
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "whisper",  // Adjust path as needed.
                    Arguments = $"\"{audioFilePath}\" --model small",
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

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        Console.WriteLine("Whisper Errors:\n" + error);
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

        /// <summary>
        /// Helper method to format the date from "yyyyMMdd" to "yyyy-MM-dd".
        /// </summary>
        static string FormatDate(string yyyyMMdd)
        {
            if (yyyyMMdd.Length != 8)
                return yyyyMMdd;
            return $"{yyyyMMdd.Substring(0, 4)}-{yyyyMMdd.Substring(4, 2)}-{yyyyMMdd.Substring(6, 2)}";
        }

        /// <summary>
        /// Helper method to sanitize a video title for use in folder names.
        /// </summary>
        static string SanitizeTitle(string title)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                title = title.Replace(c, '_');
            }
            return title;
        }
    }

    /// <summary>
    /// Class used to deserialize video metadata from yt-dlp JSON.
    /// The property names should match the JSON keys output by yt-dlp.
    /// </summary>
    class VideoMetadata
    {
        public string id { get; set; }
        public string title { get; set; }
        // upload_date is provided in "yyyyMMdd" format.
        public string upload_date { get; set; }
        // Additional fields can be added if needed.
    }
}