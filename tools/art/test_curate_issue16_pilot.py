import unittest

from PIL import Image, ImageDraw

from curate_issue16_pilot import make_ground_tile, remove_flat_background


class CurateIssue16PilotTests(unittest.TestCase):
    def test_remove_flat_background_PreservesEnclosedWhitePixels(self) -> None:
        image = Image.new("RGB", (64, 64), "white")
        draw = ImageDraw.Draw(image)
        draw.rectangle((20, 20, 43, 43), fill="firebrick")
        draw.rectangle((28, 28, 35, 35), fill="white")

        result = remove_flat_background(image)

        self.assertEqual(result.getpixel((0, 0))[3], 0)
        self.assertEqual(result.getpixel((21, 21))[3], 255)
        self.assertEqual(result.getpixel((32, 32))[3], 255)

    def test_make_ground_tile_ClipsContentToIsometricDiamond(self) -> None:
        image = Image.new("RGBA", (512, 256), (70, 150, 60, 255))

        result = make_ground_tile(image, (128, 64))

        self.assertEqual(result.size, (128, 64))
        self.assertEqual(result.getpixel((0, 0))[3], 0)
        self.assertGreater(result.getpixel((64, 32))[3], 250)

    def test_make_ground_tile_CenterCropsTextureToTileAspectRatio(self) -> None:
        image = Image.new("RGBA", (512, 512), (70, 150, 60, 255))
        draw = ImageDraw.Draw(image)
        draw.rectangle((0, 0, 511, 127), fill=(180, 50, 50, 255))
        draw.rectangle((0, 384, 511, 511), fill=(50, 50, 180, 255))

        result = make_ground_tile(image, (128, 64))

        self.assertGreater(result.getpixel((64, 32))[1], 140)


if __name__ == "__main__":
    unittest.main()
