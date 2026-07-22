# DeskCanvas

DeskCanvas は、PNG・JPEG・BMP・WebP・GIF を Windows デスクトップへ飾る
個人利用向けアプリです。画像はデスクトップアイコンより前、通常アプリより後ろに
表示されます。

## 操作

- 管理画面の「画像 / GIFを追加」、または画面へのファイルドロップで素材を追加
- `Ctrl + Alt + L`: 全体編集モードを切り替え
- 素材をドラッグ: 移動
- 右下のハンドルをドラッグ: 縦横比を保って拡大・縮小
- 上の丸いハンドルをドラッグ: 回転（Shift中は15度刻み）
- ホバーUI: 左右反転、個別ロック、削除
- 管理画面: 透明度、角度、重なり順、個別ロック、自動起動を設定
- 素材は最低64pxを画面内に残しながら、画面外へ一部はみ出して配置可能

起動時は必ず全体ロック状態です。全体ロック中と個別ロック中の素材は完全に
クリック透過となり、ホバーUIも表示しません。GIFは元のフレーム間隔で自動ループします。

## 保存

追加した素材と配置設定は `%LOCALAPPDATA%\DeskCanvas` に保存されます。
元ファイルを移動・削除しても表示は維持されます。素材をDeskCanvasから削除しても、
元ファイルは削除されません。

## 開発

必要なもの:

- Windows 10 / 11 x64
- .NET 10 SDK
- Inno Setup 7（インストーラー作成時）

```powershell
dotnet build .\DeskCanvas.slnx -c Release --configfile .\NuGet.Config
dotnet run --project .\tests\DeskCanvas.Tests\DeskCanvas.Tests.csproj -c Release --no-build

dotnet publish .\src\DeskCanvas.App\DeskCanvas.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishReadyToRun=false -p:SatelliteResourceLanguages=ja `
  -p:DebugSymbols=false -p:DebugType=None `
  -o .\artifacts\publish\win-x64 `
  --configfile .\NuGet.Config
```

インストーラーは `ISCC.exe .\installer\DeskCanvas.iss` で作成します。
