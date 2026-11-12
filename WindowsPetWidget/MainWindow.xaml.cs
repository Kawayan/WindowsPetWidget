using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsPetWidget.Models;
using WindowsPetWidget.Services;

namespace WindowsPetWidget;

public partial class MainWindow : Window
{
    private const double SmallScale = 0.75;
    private const double MediumScale = 1.0;
    private const double LargeScale = 1.5;
    private const double DirectionBoundarySlope = 0.4142135623730951;

    private readonly DispatcherTimer _animationTimer;
    private readonly SettingsService _settingsService = new();
    private readonly SpriteSheetService _spriteSheetService = new();
    private readonly Dictionary<string, List<BitmapSource>> _animationFrames =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SpriteAnimationConfig> _animationSettings =
        new(StringComparer.OrdinalIgnoreCase);

    private PetSettings _settings = new();
    private SpriteSheetConfig _spriteSheetConfig = new();
    private string _currentAnimation = string.Empty;
    private int _frameIndex;
    private int _ambientAnimationIndex;
    private DateTime _nextAmbientAnimationAt;
    private bool _isDragging;
    private Point _dragOffset;
    private double _lastWindowLeft;
    private double _lastWindowTop;

    public MainWindow()
    {
        InitializeComponent();

        _animationTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(160)
        };
        _animationTimer.Tick += AnimationTimer_Tick;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _spriteSheetConfig = _spriteSheetService.Load();
            foreach (var animation in _spriteSheetConfig.Animations)
            {
                _animationSettings.Add(animation.Id, animation);
            }

            _settings = _settingsService.Load();
            Topmost = _settings.Topmost;
            TopmostMenuItem.IsChecked = Topmost;

            LoadAnimationFrames();
            ApplyPetSize(_settings.Size);
            RestorePosition();

            SetAnimation(_spriteSheetConfig.IdleAnimation, force: true);
            _animationTimer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"スプライトシートの読み込みに失敗しました。\n\n{ex.Message}",
                "WindowsPetWidget",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
        }
    }

    private void LoadAnimationFrames()
    {
        var imagePath = _spriteSheetService.GetImagePath(_spriteSheetConfig);
        var sheet = new BitmapImage();
        sheet.BeginInit();
        sheet.UriSource = new Uri(imagePath, UriKind.Absolute);
        sheet.CacheOption = BitmapCacheOption.OnLoad;
        sheet.EndInit();
        sheet.Freeze();

        var expectedWidth = _spriteSheetConfig.Columns * _spriteSheetConfig.FrameWidth;
        var expectedHeight = _spriteSheetConfig.Rows * _spriteSheetConfig.FrameHeight;
        if (sheet.PixelWidth != expectedWidth || sheet.PixelHeight != expectedHeight)
        {
            throw new InvalidDataException(
                $"画像サイズは {expectedWidth}x{expectedHeight} の設定ですが、" +
                $"実際は {sheet.PixelWidth}x{sheet.PixelHeight} です。");
        }

        foreach (var animation in _spriteSheetConfig.Animations)
        {
            _animationFrames[animation.Id] = CropFrames(sheet, animation);
        }
    }

    private List<BitmapSource> CropFrames(BitmapSource sheet, SpriteAnimationConfig animation)
    {
        var frames = new List<BitmapSource>(animation.FrameDurations.Count);

        for (var frameIndex = 0; frameIndex < animation.FrameDurations.Count; frameIndex++)
        {
            var column = animation.StartColumn + frameIndex;
            var frame = new CroppedBitmap(
                sheet,
                new Int32Rect(
                    column * _spriteSheetConfig.FrameWidth,
                    animation.Row * _spriteSheetConfig.FrameHeight,
                    _spriteSheetConfig.FrameWidth,
                    _spriteSheetConfig.FrameHeight));
            frame.Freeze();
            frames.Add(frame);
        }

        return frames;
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        if (!_animationFrames.TryGetValue(_currentAnimation, out var frames) || frames.Count == 0)
        {
            return;
        }

        if (!_isDragging &&
            string.Equals(
                _currentAnimation,
                _spriteSheetConfig.IdleAnimation,
                StringComparison.OrdinalIgnoreCase) &&
            _spriteSheetConfig.AmbientSequence.Count > 0 &&
            DateTime.UtcNow >= _nextAmbientAnimationAt)
        {
            var animation = _spriteSheetConfig.AmbientSequence[_ambientAnimationIndex];
            _ambientAnimationIndex =
                (_ambientAnimationIndex + 1) % _spriteSheetConfig.AmbientSequence.Count;
            SetAnimation(animation);
            return;
        }

        _frameIndex++;
        if (_frameIndex >= frames.Count)
        {
            if (!_isDragging && IsAmbientAnimation(_currentAnimation))
            {
                SetAnimation(_spriteSheetConfig.IdleAnimation);
                return;
            }

            _frameIndex = 0;
        }

        PetImage.Source = frames[_frameIndex];
        SetFrameInterval(_currentAnimation, _frameIndex);
    }

    private bool IsAmbientAnimation(string animation)
    {
        return _spriteSheetConfig.AmbientSequence.Contains(
            animation,
            StringComparer.OrdinalIgnoreCase);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        _isDragging = true;
        _dragOffset = e.GetPosition(this);
        _lastWindowLeft = Left;
        _lastWindowTop = Top;
        CaptureMouse();
        e.Handled = true;
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndDrag();
            return;
        }

        var mousePosition = e.GetPosition(this);
        var newLeft = Left + mousePosition.X - _dragOffset.X;
        var newTop = Top + mousePosition.Y - _dragOffset.Y;
        var horizontalMovement = newLeft - _lastWindowLeft;
        var verticalMovement = newTop - _lastWindowTop;

        if (Math.Max(Math.Abs(horizontalMovement), Math.Abs(verticalMovement)) >= 0.5)
        {
            SetAnimation(GetDragAnimation(horizontalMovement, verticalMovement));
        }

        Left = newLeft;
        Top = newTop;
        _lastWindowLeft = newLeft;
        _lastWindowTop = newTop;
        e.Handled = true;
    }

    private string GetDragAnimation(double horizontalMovement, double verticalMovement)
    {
        var horizontalMagnitude = Math.Abs(horizontalMovement);
        var verticalMagnitude = Math.Abs(verticalMovement);

        if (verticalMagnitude <= horizontalMagnitude * DirectionBoundarySlope)
        {
            return horizontalMovement > 0
                ? _spriteSheetConfig.DragRightAnimation
                : _spriteSheetConfig.DragLeftAnimation;
        }

        if (horizontalMagnitude <= verticalMagnitude * DirectionBoundarySlope)
        {
            return verticalMovement > 0
                ? _spriteSheetConfig.DragDownAnimation
                : _spriteSheetConfig.DragUpAnimation;
        }

        if (horizontalMovement > 0)
        {
            return verticalMovement > 0
                ? _spriteSheetConfig.DragDownRightAnimation
                : _spriteSheetConfig.DragUpRightAnimation;
        }

        return verticalMovement > 0
            ? _spriteSheetConfig.DragDownLeftAnimation
            : _spriteSheetConfig.DragUpLeftAnimation;
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndDrag();
        e.Handled = true;
    }

    private void Window_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            EndDrag();
        }
    }

    private void EndDrag()
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        SetAnimation(_spriteSheetConfig.IdleAnimation, force: true);
        SaveCurrentSettings();
    }

    private void SetAnimation(string animation, bool force = false)
    {
        if (!force && string.Equals(
                _currentAnimation,
                animation,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _currentAnimation = animation;
        _frameIndex = 0;
        SetFrameInterval(animation, _frameIndex);

        if (string.Equals(
                animation,
                _spriteSheetConfig.IdleAnimation,
                StringComparison.OrdinalIgnoreCase))
        {
            _nextAmbientAnimationAt = DateTime.UtcNow +
                TimeSpan.FromMilliseconds(_spriteSheetConfig.IdleDelayMilliseconds);
        }

        if (_animationFrames.TryGetValue(animation, out var frames) && frames.Count > 0)
        {
            PetImage.Source = frames[0];
        }
    }

    private void SetFrameInterval(string animation, int frameIndex)
    {
        if (!_animationSettings.TryGetValue(animation, out var animationSetting))
        {
            throw new InvalidDataException($"アニメーション '{animation}' の設定がありません。");
        }

        _animationTimer.Interval =
            TimeSpan.FromMilliseconds(animationSetting.FrameDurations[frameIndex]);
    }

    private void TopmostMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Topmost = TopmostMenuItem.IsChecked;
        SaveCurrentSettings();
    }

    private void SizeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem menuItem ||
            menuItem.Tag is not string sizeName ||
            !Enum.TryParse(sizeName, out PetSize size))
        {
            return;
        }

        ApplyPetSize(size);
        SaveCurrentSettings();
    }

    private void ApplyPetSize(PetSize size)
    {
        var normalizedSize = size is PetSize.Small or PetSize.Medium or PetSize.Large
            ? size
            : PetSize.Medium;
        var scale = normalizedSize switch
        {
            PetSize.Small => SmallScale,
            PetSize.Large => LargeScale,
            _ => MediumScale
        };

        var width = _spriteSheetConfig.FrameWidth * scale;
        var height = _spriteSheetConfig.FrameHeight * scale;

        Width = width;
        Height = height;
        PetImage.Width = width;
        PetImage.Height = height;

        _settings.Size = normalizedSize;
        SmallSizeMenuItem.IsChecked = normalizedSize == PetSize.Small;
        MediumSizeMenuItem.IsChecked = normalizedSize == PetSize.Medium;
        LargeSizeMenuItem.IsChecked = normalizedSize == PetSize.Large;
    }

    private void ResetPositionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetDefaultPosition();
        SaveCurrentSettings();
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _animationTimer.Stop();
        SaveCurrentSettings();
    }

    private void RestorePosition()
    {
        if (_settings.Left is double left &&
            _settings.Top is double top &&
            IsVisibleOnVirtualScreen(left, top))
        {
            Left = left;
            Top = top;
            return;
        }

        SetDefaultPosition();
    }

    private void SetDefaultPosition()
    {
        Left = SystemParameters.WorkArea.Right - ActualWidth - 24;
        Top = SystemParameters.WorkArea.Bottom - ActualHeight - 24;
    }

    private bool IsVisibleOnVirtualScreen(double left, double top)
    {
        var windowRect = new Rect(left, top, ActualWidth, ActualHeight);
        var virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        return virtualScreen.IntersectsWith(windowRect);
    }

    private void SaveCurrentSettings()
    {
        if (!IsLoaded)
        {
            return;
        }

        _settings.Left = Left;
        _settings.Top = Top;
        _settings.Topmost = Topmost;
        _settingsService.Save(_settings);
    }

}
