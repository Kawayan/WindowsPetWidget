using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var options = Options.Parse(args);
            if (options.ShowHelp)
            {
                Options.PrintHelp();
                return 0;
            }

            Export(options);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"エラー: {ex.Message}");
            return 1;
        }
    }

    private static void Export(Options options)
    {
        var configPath = Path.GetFullPath(options.ConfigPath);
        var config = JsonSerializer.Deserialize<SpriteSheetConfig>(File.ReadAllText(configPath), JsonOptions)
            ?? throw new InvalidDataException("スプライト設定が空です。");

        var animationId = options.AnimationId ?? config.IdleAnimation;
        var animation = config.Animations.FirstOrDefault(item =>
            string.Equals(item.Id, animationId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"アニメーション '{animationId}' が見つかりません。");

        Validate(config, animation);

        var configDirectory = Path.GetDirectoryName(configPath)
            ?? throw new InvalidDataException("設定ファイルのフォルダーを取得できません。");
        var imagePath = Path.GetFullPath(Path.Combine(configDirectory, config.ImagePath));
        var source = LoadBitmap(imagePath);

        var expectedWidth = checked(config.Columns * config.FrameWidth);
        var expectedHeight = checked(config.Rows * config.FrameHeight);
        if (source.PixelWidth != expectedWidth || source.PixelHeight != expectedHeight)
        {
            throw new InvalidDataException(
                $"画像サイズは {source.PixelWidth}x{source.PixelHeight} ですが、設定では {expectedWidth}x{expectedHeight} です。");
        }

        var encoder = new GifBitmapEncoder();
        for (var index = 0; index < animation.FrameDurations.Count; index++)
        {
            var column = animation.StartColumn + index;
            var crop = new CroppedBitmap(source, new Int32Rect(
                column * config.FrameWidth,
                animation.Row * config.FrameHeight,
                config.FrameWidth,
                config.FrameHeight));

            var metadata = new BitmapMetadata("gif");
            metadata.SetQuery("/grctlext/Delay", ToGifDelay(animation.FrameDurations[index]));
            metadata.SetQuery("/grctlext/Disposal", (byte)2);
            encoder.Frames.Add(BitmapFrame.Create(crop, null, metadata, null));
        }

        var outputPath = Path.GetFullPath(options.OutputPath ?? Path.Combine("docs", $"{animation.Id}.gif"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using (var output = File.Create(outputPath))
        {
            encoder.Save(output);
        }

        PatchGifAnimationMetadata(outputPath, animation.FrameDurations);

        Console.WriteLine($"作成しました: {outputPath}");
        Console.WriteLine($"アニメーション: {animation.Id} ({animation.FrameDurations.Count}フレーム)");
    }

    private static BitmapSource LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static ushort ToGifDelay(int milliseconds)
    {
        var centiseconds = Math.Max(1, (milliseconds + 5) / 10);
        return checked((ushort)centiseconds);
    }

    private static void PatchGifAnimationMetadata(string path, IReadOnlyList<int> durations)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 13 || Encoding.ASCII.GetString(bytes, 0, 3) != "GIF")
        {
            throw new InvalidDataException("生成されたファイルがGIF形式ではありません。");
        }

        var position = 13;
        if ((bytes[10] & 0x80) != 0)
        {
            position += 3 * (1 << ((bytes[10] & 0x07) + 1));
        }

        byte[] infiniteLoopExtension =
        [
            0x21, 0xFF, 0x0B,
            0x4E, 0x45, 0x54, 0x53, 0x43, 0x41, 0x50, 0x45, 0x32, 0x2E, 0x30,
            0x03, 0x01, 0x00, 0x00, 0x00
        ];
        var bytesWithLoop = new byte[bytes.Length + infiniteLoopExtension.Length];
        Buffer.BlockCopy(bytes, 0, bytesWithLoop, 0, position);
        Buffer.BlockCopy(
            infiniteLoopExtension,
            0,
            bytesWithLoop,
            position,
            infiniteLoopExtension.Length);
        Buffer.BlockCopy(
            bytes,
            position,
            bytesWithLoop,
            position + infiniteLoopExtension.Length,
            bytes.Length - position);
        bytes = bytesWithLoop;
        position += infiniteLoopExtension.Length;

        var frameIndex = 0;
        while (position < bytes.Length)
        {
            switch (bytes[position])
            {
                case 0x3B:
                    position = bytes.Length;
                    break;
                case 0x21 when position + 7 < bytes.Length && bytes[position + 1] == 0xF9 && bytes[position + 2] == 4:
                    if (frameIndex >= durations.Count)
                    {
                        throw new InvalidDataException("GIFのフレーム数が設定より多くなっています。");
                    }

                    var delay = ToGifDelay(durations[frameIndex++]);
                    bytes[position + 3] = (byte)((bytes[position + 3] & 0xE3) | (2 << 2));
                    bytes[position + 4] = (byte)(delay & 0xFF);
                    bytes[position + 5] = (byte)(delay >> 8);
                    position += 8;
                    break;
                case 0x21:
                    position += 2;
                    SkipSubBlocks(bytes, ref position);
                    break;
                case 0x2C:
                    if (position + 9 >= bytes.Length)
                    {
                        throw new InvalidDataException("GIFの画像記述子が途中で終了しています。");
                    }

                    var packed = bytes[position + 9];
                    position += 10;
                    if ((packed & 0x80) != 0)
                    {
                        position += 3 * (1 << ((packed & 0x07) + 1));
                    }

                    position++;
                    SkipSubBlocks(bytes, ref position);
                    break;
                default:
                    throw new InvalidDataException($"GIF内に不明なブロック 0x{bytes[position]:X2} があります。");
            }
        }

        if (frameIndex != durations.Count)
        {
            throw new InvalidDataException($"GIFは{frameIndex}フレームですが、設定は{durations.Count}フレームです。");
        }

        File.WriteAllBytes(path, bytes);
    }

    private static void SkipSubBlocks(byte[] bytes, ref int position)
    {
        while (position < bytes.Length)
        {
            var length = bytes[position++];
            if (length == 0)
            {
                return;
            }

            position += length;
            if (position > bytes.Length)
            {
                break;
            }
        }

        throw new InvalidDataException("GIFのデータブロックが途中で終了しています。");
    }

    private static void Validate(SpriteSheetConfig config, SpriteAnimationConfig animation)
    {
        if (config.Columns <= 0 || config.Rows <= 0 || config.FrameWidth <= 0 || config.FrameHeight <= 0)
        {
            throw new InvalidDataException("行列数とフレームサイズは1以上にしてください。");
        }

        if (animation.Row < 0 || animation.Row >= config.Rows || animation.StartColumn < 0 ||
            animation.FrameDurations.Count == 0 ||
            animation.StartColumn + animation.FrameDurations.Count > config.Columns)
        {
            throw new InvalidDataException($"アニメーション '{animation.Id}' のフレーム範囲が不正です。");
        }

        if (animation.FrameDurations.Any(duration => duration <= 0))
        {
            throw new InvalidDataException($"アニメーション '{animation.Id}' の表示時間は1ms以上にしてください。");
        }
    }

    private sealed class Options
    {
        public string ConfigPath { get; private set; } = @"WindowsPetWidget\Assets\spritesheet.json";
        public string? AnimationId { get; private set; }
        public string? OutputPath { get; private set; }
        public bool ShowHelp { get; private set; }

        public static Options Parse(string[] args)
        {
            var options = new Options();
            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--config":
                        options.ConfigPath = ReadValue(args, ref index, "--config");
                        break;
                    case "--animation":
                        options.AnimationId = ReadValue(args, ref index, "--animation");
                        break;
                    case "--output":
                        options.OutputPath = ReadValue(args, ref index, "--output");
                        break;
                    case "--help" or "-h":
                        options.ShowHelp = true;
                        break;
                    default:
                        throw new ArgumentException($"不明な引数です: {args[index]}");
                }
            }

            return options;
        }

        public static void PrintHelp()
        {
            Console.WriteLine("スプライトシートのアニメーションをGIFへ変換します。");
            Console.WriteLine();
            Console.WriteLine("オプション:");
            Console.WriteLine("  --config <path>       spritesheet.json のパス");
            Console.WriteLine("  --animation <id>      アニメーションID（省略時は idleAnimation）");
            Console.WriteLine("  --output <path>       出力GIFのパス（省略時は docs\\<id>.gif）");
            Console.WriteLine("  --help, -h            このヘルプを表示");
        }

        private static string ReadValue(string[] args, ref int index, string option)
        {
            if (++index >= args.Length)
            {
                throw new ArgumentException($"{option} の値を指定してください。");
            }

            return args[index];
        }
    }

    private sealed class SpriteSheetConfig
    {
        public string ImagePath { get; set; } = "spritesheet.png";
        public int Columns { get; set; }
        public int Rows { get; set; }
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }
        public string IdleAnimation { get; set; } = "idle";
        public List<SpriteAnimationConfig> Animations { get; set; } = [];
    }

    private sealed class SpriteAnimationConfig
    {
        public string Id { get; set; } = string.Empty;
        public int Row { get; set; }
        public int StartColumn { get; set; }
        public List<int> FrameDurations { get; set; } = [];
    }
}
