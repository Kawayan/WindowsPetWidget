using System.IO;
using System.Text.Json;
using WindowsPetWidget.Models;

namespace WindowsPetWidget.Services;

public sealed class SpriteSheetService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string _assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");

    public SpriteSheetConfig Load()
    {
        var configPath = Path.Combine(_assetsDirectory, "spritesheet.json");

        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<SpriteSheetConfig>(json, JsonOptions)
                ?? throw new InvalidDataException("設定ファイルが空です。");

            Validate(config);
            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"spritesheet.json の形式が正しくありません: {ex.Message}", ex);
        }
    }

    public string GetImagePath(SpriteSheetConfig config)
    {
        if (Path.IsPathRooted(config.ImagePath) || config.ImagePath.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidDataException("imagePath は Assets フォルダー内の相対パスを指定してください。");
        }

        return Path.Combine(_assetsDirectory, config.ImagePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void Validate(SpriteSheetConfig config)
    {
        if (config.Columns <= 0 || config.Rows <= 0 ||
            config.FrameWidth <= 0 || config.FrameHeight <= 0)
        {
            throw new InvalidDataException("columns、rows、frameWidth、frameHeight は1以上にしてください。");
        }

        if (config.IdleDelayMilliseconds <= 0)
        {
            throw new InvalidDataException("idleDelayMilliseconds は1以上にしてください。");
        }

        if (string.IsNullOrWhiteSpace(config.ImagePath))
        {
            throw new InvalidDataException("imagePath を指定してください。");
        }

        if (config.Animations is null || config.Animations.Count == 0)
        {
            throw new InvalidDataException("animations を1件以上定義してください。");
        }

        if (config.AmbientSequence is null)
        {
            throw new InvalidDataException("ambientSequence は配列で指定してください。");
        }

        var animationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var animation in config.Animations)
        {
            if (string.IsNullOrWhiteSpace(animation.Id) || !animationIds.Add(animation.Id))
            {
                throw new InvalidDataException("アニメーションIDは空欄や重複のない値にしてください。");
            }

            if (animation.Row < 0 || animation.Row >= config.Rows)
            {
                throw new InvalidDataException($"{animation.Id}: row がシートの範囲外です。");
            }

            if (animation.FrameDurations is null ||
                animation.StartColumn < 0 || animation.FrameDurations.Count == 0 ||
                animation.StartColumn + animation.FrameDurations.Count > config.Columns)
            {
                throw new InvalidDataException($"{animation.Id}: 使用する列がシートの範囲外です。");
            }

            if (animation.FrameDurations.Any(duration => duration <= 0))
            {
                throw new InvalidDataException($"{animation.Id}: frameDurations はすべて1以上にしてください。");
            }
        }

        ValidateAnimationReference(config.IdleAnimation, "idleAnimation", animationIds);
        ValidateAnimationReference(config.DragRightAnimation, "dragRightAnimation", animationIds);
        ValidateAnimationReference(config.DragLeftAnimation, "dragLeftAnimation", animationIds);
        ValidateAnimationReference(config.DragUpAnimation, "dragUpAnimation", animationIds);
        ValidateAnimationReference(config.DragDownAnimation, "dragDownAnimation", animationIds);
        ValidateAnimationReference(config.DragUpRightAnimation, "dragUpRightAnimation", animationIds);
        ValidateAnimationReference(config.DragDownRightAnimation, "dragDownRightAnimation", animationIds);
        ValidateAnimationReference(config.DragUpLeftAnimation, "dragUpLeftAnimation", animationIds);
        ValidateAnimationReference(config.DragDownLeftAnimation, "dragDownLeftAnimation", animationIds);

        foreach (var animationId in config.AmbientSequence)
        {
            ValidateAnimationReference(animationId, "ambientSequence", animationIds);
        }
    }

    private static void ValidateAnimationReference(
        string animationId,
        string propertyName,
        HashSet<string> animationIds)
    {
        if (string.IsNullOrWhiteSpace(animationId) || !animationIds.Contains(animationId))
        {
            throw new InvalidDataException($"{propertyName} の '{animationId}' は animations に定義されていません。");
        }
    }
}
