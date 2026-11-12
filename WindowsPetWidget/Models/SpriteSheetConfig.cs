namespace WindowsPetWidget.Models;

public sealed class SpriteSheetConfig
{
    public string ImagePath { get; set; } = "spritesheet.png";

    public int Columns { get; set; }

    public int Rows { get; set; }

    public int FrameWidth { get; set; }

    public int FrameHeight { get; set; }

    public int IdleDelayMilliseconds { get; set; } = 5000;

    public string IdleAnimation { get; set; } = "idle";

    public string DragRightAnimation { get; set; } = "running-right";

    public string DragLeftAnimation { get; set; } = "running-left";

    public string DragUpAnimation { get; set; } = "idle";

    public string DragDownAnimation { get; set; } = "idle";

    public string DragUpRightAnimation { get; set; } = "running-right";

    public string DragDownRightAnimation { get; set; } = "running-right";

    public string DragUpLeftAnimation { get; set; } = "running-left";

    public string DragDownLeftAnimation { get; set; } = "running-left";

    public List<string> AmbientSequence { get; set; } = [];

    public List<SpriteAnimationConfig> Animations { get; set; } = [];
}

public sealed class SpriteAnimationConfig
{
    public string Id { get; set; } = string.Empty;

    public int Row { get; set; }

    public int StartColumn { get; set; }

    public List<int> FrameDurations { get; set; } = [];
}
