# Credits

Third-party assets used in the game. Each one was selected by the owner and is released under Creative Commons Zero (CC0, a public domain dedication), so credit is not legally required; it is given here anyway. The audio approach is recorded in [ADR 0018](docs/adr/0018-audio-approach.md).

## Audio

Files live under `TransportTycoon2/Assets/_Project/Audio/` and keep their original file names, except the click: `click_001_padded.wav` is `click_001.ogg` decoded to 16-bit WAV with 30 ms of silence added at the start. The click's whole sound sits in its first 10 ms, which the AAC encoder of the Unity Web player dropped (the click was nearly silent in WebGL builds); after the silence it encodes intact.

| Use in game | File | Asset | Author | License |
| --- | --- | --- | --- | --- |
| UI click and map selection | `Sfx/click_001_padded.wav` | [Interface Sounds](https://kenney.nl/assets/interface-sounds) (1.0), `click_001.ogg` | Kenney ([kenney.nl](https://kenney.nl)) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Build | `Sfx/impactPlank_medium_000.ogg` | [Impact Sounds](https://kenney.nl/assets/impact-sounds) (1.0) | Kenney ([kenney.nl](https://kenney.nl)) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Destroy | `Sfx/impactPlate_heavy_000.ogg` | [Impact Sounds](https://kenney.nl/assets/impact-sounds) (1.0) | Kenney ([kenney.nl](https://kenney.nl)) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Cash in (delivery paid) | `Sfx/snd_purchase.wav` | [Purchasing sound effect](https://opengameart.org/content/purchasing-sound-effect) | Spring Spring | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Ambient music | `Music/LofiLoop.ogg` | [Lofi Hip Hop Loop](https://opengameart.org/content/lofi-hip-hop-loop) | omfgdude (OMF-Games) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
