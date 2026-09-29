import array
import sys
import wave

import torch


def main():
    model_path, output_path, speaker = sys.argv[1:4]
    text = sys.stdin.read().strip()
    if not text:
        raise ValueError("Пустая фраза")
    torch.set_num_threads(2)
    model = torch.package.PackageImporter(model_path).load_pickle("tts_models", "model")
    audio = model.apply_tts(text=text, speaker=speaker, sample_rate=24000)
    samples = array.array("h", (max(-32768, min(32767, int(x * 32767))) for x in audio.tolist()))
    with wave.open(output_path, "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(24000)
        output.writeframes(samples.tobytes())


if __name__ == "__main__":
    main()
