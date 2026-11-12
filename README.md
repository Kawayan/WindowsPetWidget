# WindowsPetWidget

透過PNGのスプライトアニメーションで動く、Windows向けデスクトップペットです。
キャラクターはドラッグして好きな場所へ移動できます。

![待機アニメーション](docs/idle.gif)

## 主な機能

- 透過・枠なしウィンドウでデスクトップ上にキャラクターを表示
- JSONで定義したフレーム時間に沿ったスプライトアニメーション
- ドラッグ方向に応じた移動アニメーション
- 待機中に「手を振る」「ジャンプ」「作業中」などのアニメーションを順番に再生
- 大・中・小の表示サイズ切り替え
- 最前面表示のオン／オフ
- ウィンドウ位置、表示サイズ、最前面設定の自動保存
- スプライトアニメーションから透過GIFを生成するツール

## 動作環境

- Windows x64
- 開発時: .NET SDK 10.0.301

配布用ビルドは自己完結型の単一EXEとして作成されるため、実行先に.NETランタイムを別途導入する必要はありません。

## 実行方法

リポジトリのルートで次のコマンドを実行します。

```powershell
dotnet run --project .\WindowsPetWidget\WindowsPetWidget.csproj
```

## 操作方法

| 操作 | 動作 |
| --- | --- |
| 左ドラッグ | ペットを移動し、移動方向に対応するアニメーションを再生 |
| 右クリック | コンテキストメニューを表示 |
| 常に手前に表示 | 最前面表示を切り替え |
| サイズ | 大・中・小から表示サイズを選択 |
| 位置をリセット | ペットを画面右下へ戻す |
| 終了 | 現在の設定を保存してアプリを終了 |

設定は次のファイルへ保存されます。

```text
%LocalAppData%\WindowsPetWidget\settings.json
```

保存した位置が現在の画面範囲外にある場合は、次回起動時に画面右下へ戻ります。

## ビルド

ソリューション全体をビルドします。

```powershell
dotnet build .\WindowsPetWidget.slnx
```

ビルド成果物は `artifacts\bin` 以下に生成されます。

## Publish

登録済みのフォルダーPublish Profileを使用します。

```powershell
dotnet publish .\WindowsPetWidget\WindowsPetWidget.csproj -p:PublishProfile=FolderProfile
```

成果物は次のフォルダーに生成されます。

```text
artifacts\publish\WindowsPetWidget\release_win-x64\
├── WindowsPetWidget.exe
└── Assets\
    ├── spritesheet.json
    └── spritesheet.png
```

`spritesheet.png` は単一EXEへ埋め込まず、差し替え可能な外部ファイルとしてPublishされます。

## スプライトアニメーションの設定

アニメーションは次の2ファイルで構成されています。

- `WindowsPetWidget\Assets\spritesheet.png`: 透過PNGスプライトシート
- `WindowsPetWidget\Assets\spritesheet.json`: シート構成とアニメーション定義

主なJSON設定は次のとおりです。

| 項目 | 内容 |
| --- | --- |
| `imagePath` | JSONと同じフォルダーにある画像の相対パス |
| `columns` / `rows` | スプライトシートの列数と行数 |
| `frameWidth` / `frameHeight` | 1フレームのピクセルサイズ |
| `idleDelayMilliseconds` | 特別な待機アニメーションへ切り替わるまでの時間 |
| `idleAnimation` | 通常待機に使用するアニメーションID |
| `drag*Animation` | 8方向のドラッグに使用するアニメーションID |
| `ambientSequence` | 待機中に順番に再生するアニメーションID |
| `animations` | 各アニメーションの行、開始列、フレーム表示時間 |

`row` と `startColumn` は0から数えます。`frameDurations` は各フレームの表示時間をミリ秒で指定し、その要素数がアニメーションのフレーム数になります。

現在のスプライトシートは8列×9行、1フレーム192×208ピクセルです。次のアニメーションを定義しています。

| ID | 用途 |
| --- | --- |
| `idle` | 通常待機 |
| `running-right` / `running-left` | 左右への移動 |
| `waving` | 手を振る |
| `jumping` | ジャンプ |
| `failed` | 失敗 |
| `waiting` | 待機 |
| `working` | 作業中 |
| `review` | レビュー中 |

画像の実寸とJSONから計算したサイズが一致しない場合や、アニメーション定義が不正な場合は、起動時にエラーを表示して終了します。

## GIFの生成

`SpriteSheetGif` ツールは、JSONのフレーム範囲と表示時間を使って、残像のない無限ループの透過アニメーションGIFを生成します。

待機アニメーションを `docs\idle.gif` へ出力するには、リポジトリのルートで次を実行します。

```powershell
dotnet run --project .\tools\SpriteSheetGif\SpriteSheetGif.csproj
```

別のアニメーションを生成する場合はIDを指定します。出力先を省略すると `docs\<アニメーションID>.gif` になります。

```powershell
dotnet run --project .\tools\SpriteSheetGif\SpriteSheetGif.csproj -- --animation waving
```

設定ファイルや出力先も変更できます。

```powershell
dotnet run --project .\tools\SpriteSheetGif\SpriteSheetGif.csproj -- `
  --config .\WindowsPetWidget\Assets\spritesheet.json `
  --animation idle `
  --output .\docs\idle.gif
```

利用可能なオプションは次のコマンドで確認できます。

```powershell
dotnet run --project .\tools\SpriteSheetGif\SpriteSheetGif.csproj -- --help
```

## プロジェクト構成

```text
WindowsPetWidget.slnx
├── WindowsPetWidget\            デスクトップペット本体
│   ├── Assets\                  スプライト画像とJSON設定
│   ├── Models\                  設定モデル
│   ├── Services\                スプライト／ユーザー設定の読み書き
│   └── Properties\PublishProfiles\
├── tools\SpriteSheetGif\        アニメーションGIF生成ツール
└── docs\idle.gif                README用の待機アニメーション
```

## ライセンス

WindowsPetWidget は MIT ライセンスで公開しています。詳細は [LICENSE](LICENSE) を参照してください。
