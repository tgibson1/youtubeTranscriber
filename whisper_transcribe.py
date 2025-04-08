# whisper_transcribe.py
import sys
import whisper

def transcribe(audio_path: str) -> str:
    # You can choose 'tiny', 'base', 'small', 'medium', or 'large'
    model = whisper.load_model("base")
    result = model.transcribe(audio_path)
    return result["text"]

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python whisper_transcribe.py <audio_file>")
        sys.exit(1)
    audio_file = sys.argv[1]
    transcription = transcribe(audio_file)
    print(transcription)