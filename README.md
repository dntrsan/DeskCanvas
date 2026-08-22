# DeskCanvas 1.1.0

DeskCanvas は画像・GIF・時計・再生中・システムモニター・Codexリミットを Windows デスクトップへ置く個人利用向けアプリです。項目はデスクトップアイコンより前、通常アプリより後ろに表示されます。

「Codexリミット」はCodexのローカルApp Serverから、同じログイン状態の残量、対象期間、リセット日時、プラン、利用可能なクレジットを読み取り専用で表示します。ライブ取得に失敗した場合は、ローカルのCodexセッション履歴に残る最新値へ切り替え、オフライン表示と最終更新時刻を併記します。ローカルトークン数から公式残量を推測することはありません。

「再生中」は Windows 10 1809 以降の Global System Media Transport Controls を読んで、現在のセッションの曲名、アーティスト、アルバムアート、再生状態、進捗を表示します。前へ・再生/一時停止・次へと、対応する場合はシークを使えます。再生アプリが無いときは「再生中のコンテンツはありません」と表示されます。再生情報はWindowsが公開するセッションに限られ、アプリによってはアート、進捗、操作の一部を公開しません。

システムモニターは共有のバックグラウンドサンプラーで、CPU全体、物理メモリ、GPU、物理ネットワーク回線の下り/上りを毎秒表示します。GPUは `GPU Engine` のプロセス別カウンターをLUID・物理アダプター・エンジン番号・エンジン種別でまとめ、同じ物理エンジンへの寄与を合算してから100%に丸め、最も忙しいエンジンを表示します。ネットワークは稼働中のhardware interfaceだけを合算し、loopback・tunnel・virtual interfaceの二重計上を避けます。初回値と一時的な取得失敗は「取得中」、Windowsがカウンターを公開していない場合だけ `--` です。

## 操作

- 管理画面から画像 / GIF、または「標準コンテンツを追加」で時計・再生中・システムモニターを追加
- 時計は Digital / Split / Analog、12/24時間、秒、年、月日を選べる
- `Ctrl + Alt + L`: 全体編集モードを切り替え
- 編集中はドラッグで移動、右下で拡大縮小、上で回転（Shiftで15度刻み）
- 通常時、再生中ウィジェットはメディア操作だけをクリックで受け、背景や文字のクリックはデスクトップへ通す
- 一時非表示中はGIFを止め、ライブ項目の共有サービス購読も停止する

配置、装飾、ロック、重なり順、ウィジェット設定は `%LOCALAPPDATA%\DeskCanvas` の layout v2 に保存されます。一時非表示は起動中のみです。組み込み項目を削除しても画像ファイルは削除しません。

## 開発

Windows 10 / 11 x64 と .NET 10 SDK が必要です。

```powershell
C:\tmp\dotnet10\dotnet.exe build .\DeskCanvas.slnx -c Release --configfile .\NuGet.Config
C:\tmp\dotnet10\dotnet.exe run --project .\tests\DeskCanvas.Tests\DeskCanvas.Tests.csproj -c Release --no-build
```
