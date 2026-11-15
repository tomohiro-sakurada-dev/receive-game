# receive-game
A challenging 3D game designed to train accurate information reception and listening skills.

## PROJECT RECEIVE — 霧音の国

霧が文字を溶かす山国で、人の言葉を聞き、歪めずに運ぶ「聴き継ぎ」になる。聞き終える前に分かったつもりになると失敗する、情報受信の修行ゲーム。

いまあるのは**第2段階の試作版（文字ベース）**。3D はまだない。

### 現在の状態（2026-11-15）

| 項目 | 状態 |
|---|---|
| ゲームの中核（シナリオ生成・会話・評価・罰・技能の記録） | 実装済み。自動テスト 60 件が通る |
| 端末版（同じ中核を文字だけで遊ぶ） | 動作確認済み。成就・未達・歪みの3通りを最後まで遊んだ |
| Unity プロジェクト（URP、UI Toolkit の画面） | 作成済み。**Unity Editor での動作は未確認**（開発環境に Unity が無かった） |
| AI・通信 | 使わない。すべて端末内で動く |

未確認の点は `docs/reports/stage2-implementation-report.md` にまとめてある。

## Unity で遊ぶ

### 必要なもの

- Unity 6.3 LTS（6000.3 系）。Unity Hub から入れる
- 追加の素材やアカウント設定は不要

### 手順

1. Unity Hub の「Add」→「Add project from disk」で、このリポジトリのフォルダを選ぶ。
2. Hub が Editor の版を尋ねたら、入っている 6000.3 系を選ぶ（`ProjectSettings/ProjectVersion.txt` は `6000.3.0f1` と書いてあるが、同じ 6000.3 系なら動く想定）。
3. 初回は、パッケージの取得と取り込みに数分かかる。終わると、エディタ用スクリプトが起動用シーン `Assets/Scenes/Receive.unity` と URP の設定 `Assets/Settings/` を自動で作る。
4. 再生ボタンを押す。題字の画面が出れば成功。

どのシーンを開いていても、再生すればゲームが始まる（`Bootstrap` が起動する）。手順3の自動作成に失敗しても遊べる。作り直したいときは、メニューの「RECEIVE > プロジェクトを整える（シーンと URP）」を選ぶ。

### 初回に開いた後でやること

- Unity が作った `.meta` ファイル、`Assets/Scenes/`、`Assets/Settings/`、`ProjectSettings/` の中身をコミットする（リポジトリにはまだ入っていない）。
- Console に赤いエラーが出ていたら、その内容を控える。報告書の「未確認の点」のどれかに当たるはずである。

### Unity でテストを走らせる

Window > General > Test Runner の EditMode で `Receive.Tests` を実行する。

## Unity なしで遊ぶ・検査する

.NET 8 SDK があれば、Unity と同じ中核を端末で動かせる。

```sh
# 遊ぶ（端末版）
dotnet run --project dotnet/Receive.Console

# 自動テスト（60 件）
dotnet test dotnet/Receive.Tests

# シナリオを正解つきで眺める（例: 第3の地を5本）
dotnet run --project dotnet/Receive.Console -- sample 5 3

# Unity 側のコードのコンパイル検査（Unity 2021.3 の参照アセンブリに対して。実行はしない）
dotnet build dotnet/Receive.UnityCheck
```

## 遊び方

1. **聴く** — 話し手の言葉は一文字ずつ現れ、語り終えると消える。読み返せない。「続きを聴く」で話し手が終わりを告げるまで、話は終わっていない。
2. **聞き返す** — 確かめたいことを訊ける。回数には限りがある。これから語られることを先に訊くと、話の腰を折る。
3. **整理する** — 八つの欄（相手・用向き・品・数・日・刻・場所・ただし書き）すべてに印を付ける。聞いた／推し量り／分からない／言われていない。
4. **申し送る** — 受け手が言い換えて確かめてくる。話し手の言葉と違えば、流されずに正す。
5. **判定** — 話し手が実際に言った言葉と、自分が渡した言葉が並ぶ。

| 結果 | 意味 |
|---|---|
| 成就 | 言葉が歪まずに届いた。先へ進む |
| 未達 | 偽りは渡していないが、肝心なことが欠けた。罰はなく、別の話でやり直す |
| 歪み | 誤った言葉を確かなこととして渡した。結び灯籠まで戻される |

分からないことを「分からない」と言うのに罰はない。言われていないことを「聞いた」として渡すのが、最も重い誤りである。

聴き帳には、七つの技能（最後まで聞く／正確に受け取る／補わない／事実と推測を分ける／分からないと知る／時を得て問う／歪めずに伝える）が別々に記録される。

## フォルダの構成

| 場所 | 中身 |
|---|---|
| `Assets/Receive/Core/` | ゲームの中核。Unity にも通信にも依存しない C# |
| `Assets/Receive/Game/` | Unity の画面。起動、背景の描画、各画面 |
| `Assets/Receive/Resources/` | 画面の見た目（USS）、和文フォント |
| `Assets/Receive/Editor/` | 初回の準備（シーンと URP の設定を作る） |
| `Assets/Receive/Tests/` | 自動テスト。Unity でも .NET でも同じファイルを使う |
| `dotnet/` | Unity の外で中核をビルド・テスト・実行するためのプロジェクト |
| `docs/design/` | 第1段階の設計書 |
| `docs/decisions/` | 実装中の判断と、その理由 |
| `docs/reports/` | 実装・テスト報告書、テスト結果、実プレイの記録 |

## ライセンスについて

同梱の和文フォント「しっぽり明朝」は SIL Open Font License 1.1 で配布されている。ライセンス文は `Assets/Receive/Resources/Fonts/OFL.txt` にある。
