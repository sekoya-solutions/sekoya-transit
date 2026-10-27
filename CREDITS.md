# Credits

Third-party assets used in the game. Each one was selected by the owner and is released under Creative Commons Zero (CC0, a public domain dedication), so credit is not legally required; it is given here anyway. The audio approach is recorded in [ADR 0018](docs/adr/0018-audio-approach.md), the map art approach in [ADR 0032](docs/adr/0032-map-art-from-kenney-cc0-packs.md).

## Audio

Files live under `TransportTycoon2/Assets/_Project/Audio/` and keep their original file names, except the click: `click_001_padded.wav` is `click_001.ogg` decoded to 16-bit WAV with 30 ms of silence added at the start. The click's whole sound sits in its first 10 ms, which the AAC encoder of the Unity Web player dropped (the click was nearly silent in WebGL builds); after the silence it encodes intact.

| Use in game | File | Asset | Author | License |
| --- | --- | --- | --- | --- |
| UI click and map selection | `Sfx/click_001_padded.wav` | [Interface Sounds](https://kenney.nl/assets/interface-sounds) (1.0), `click_001.ogg` | Kenney ([kenney.nl](https://kenney.nl)) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Build | `Sfx/impactPlank_medium_000.ogg` | [Impact Sounds](https://kenney.nl/assets/impact-sounds) (1.0) | Kenney ([kenney.nl](https://kenney.nl)) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Destroy | `Sfx/impactPlate_heavy_000.ogg` | [Impact Sounds](https://kenney.nl/assets/impact-sounds) (1.0) | Kenney ([kenney.nl](https://kenney.nl)) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Cash in (delivery paid) | `Sfx/snd_purchase.wav` | [Purchasing sound effect](https://opengameart.org/content/purchasing-sound-effect) | Spring Spring | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Ambient music | `Music/LofiLoop.ogg` | [Lofi Hip Hop Loop](https://opengameart.org/content/lofi-hip-hop-loop) | omfgdude (OMF-Games) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |

## Map art

The source files live under `TransportTycoon2/Assets/_Project/Art/Map/Source/`, one folder per pack with its license. The game uses sprites baked from them by an editor tool (3D models rendered at the map's angle, road tiles cropped to the tile diamond; see the [art pipeline](docs/specs/art-pipeline.md#map-art-issue-69)). All are by Kenney ([kenney.nl](https://kenney.nl)).

| Use in game | Files used | Asset | License |
| --- | --- | --- | --- |
| Rails, locomotive, wagon | `track`, `railroad-rail-straight`, `train-locomotive-c`, `train-carriage-container-blue` | [Train Kit](https://kenney.nl/assets/train-kit) (1.1) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Truck | `delivery` | [Car Kit](https://kenney.nl/assets/car-kit) (3.1) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Stations, industries | `building-a`, `building-o`, `building-s`, `building-m`, `building-e`, `building-j` | [City Kit Industrial](https://kenney.nl/assets/city-kit-industrial) (2.0) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Town houses | `building-type-a`, `-c`, `-f`, `-m` | [City Kit Suburban](https://kenney.nl/assets/city-kit-suburban) (2.0) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Roads, forest trees | 15 road tiles, `coniferTall`, `coniferAltTall` | [Isometric Roads](https://kenney.nl/assets/isometric-roads) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| Terrain colors (no files) | Top-face colors of tiles 067, 059, 083 | [Isometric Landscape](https://kenney.nl/assets/isometric-tiles-landscape) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
