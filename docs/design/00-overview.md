# PROJECT RECEIVE 設計書 — 00 総覧・調査結果・前提

- 段階: 第1段階（ゲーム定義・設計）。**コード・Unityプロジェクトは一切作成していない。**
- 作成日: 2026-11-15
- 状態: **承認待ち**。承認されるまで第2段階には進まない。

## 1. 成果物一覧

| No. | 文書 | 内容 |
|---|---|---|
| 00 | 本書 | 調査結果、技術検証、用語、承認前の要確認事項 |
| 01 | [基本コンセプト](01-concept.md) | 何を鍛えるか、なぜ面白いか、世界観 |
| 02 | [ゲームシステム設計書](02-game-system.md) | 基本サイクル、試練の型、進行、難易度 |
| 03 | [罰システム設計書](03-penalty-system.md) | ミス分類、判定規則、公平性の保証 |
| 04 | [会話生成システム設計書](04-conversation-generation.md) | シナリオ生成、初見限定、会話役 |
| 05 | [AI評価システム設計書](05-ai-evaluation.md) | 評価基準票、評価役、誤判定対策 |
| 06 | [音声システム設計書](06-voice-system.md) | 音声認識・合成、発話終了検出、検証 |
| 07 | [Unityの技術構成](07-unity-architecture.md) | バージョン、描画、構成、料金、セキュリティ |
| 08 | [画面・ステージ構成](08-screens-stages.md) | 画面遷移、地域、演出 |
| 09 | [データ設計](09-data-design.md) | 保存データ、保持期間、整合性 |
| 10 | [開発工程](10-roadmap.md) | 段階、判定ゲート、成果物 |
| 11 | [テスト計画](11-test-plan.md) | 評価精度、音声、性能、学習効果 |
| 12 | [技術的リスクと対策](12-risks.md) | リスク一覧と対策 |
| 13 | [品質監査報告](13-quality-audit.md) | 独立した立場からの欠陥指摘 |

## 2. 既存環境の調査結果

| 項目 | 結果 |
|---|---|
| リポジトリ | `receive-game`。コミットは「Initial commit」1件のみ |
| 既存ファイル | `README.md`（1行の説明）と `.gitignore`（Unity標準テンプレート）の2つだけ |
| Unityプロジェクト | **存在しない**（`Assets/`、`ProjectSettings/`、`Packages/` なし） |
| 既存のコード・データ | なし |
| この作業環境 | クラウド上の一時コンテナ。**Unity Editor も .NET SDK も入っていない**（Node 22 と Python 3.13 のみ） |
| 今回の変更 | `docs/design/` に本設計書14ファイルを**新規追加**しただけ。既存2ファイルは未変更 |

重要な帰結: この環境では Unity のコードをコンパイルも実行もできない。第2段階以降は、Unity を入れた手元のPC上で Claude Code を動かすか、Unity ライセンス付きの CI を用意する必要がある（[07](07-unity-architecture.md) §9、[12](12-risks.md) R-14）。

## 3. 技術検証の結果

凡例: **確認済** = 公式の資料で確認した。**第三者情報** = 公式以外の記事で、数値が食い違うものもある。**未検証** = 今回は確認できていない。

### 3.1 Claude Fable 5.2

出典は Anthropic の API リファレンス（2026-11-03 時点の版）。**実際に API を呼び出す疎通確認は行っていない**（APIキーを使う作業は今回の範囲外）。

| 項目 | 結果 | 設計への影響 |
|---|---|---|
| API での利用 | **確認済**: モデルID `claude-fable-5-2` で利用できる | 会話生成・評価の中心に使える |
| 料金 | **確認済**: 入力 $8、出力 $40（100万トークンあたり）。キャッシュ読み出し $0.20、バッチ $4 / $20 | 全工程を Fable で都度実行すると高額（[07](07-unity-architecture.md) §7） |
| 音声の入出力 | **対応の記載なし**。Messages API の入力はテキスト・画像・文書。ウェブ検索でも対応の証拠は見つからなかった | 音声認識・音声合成は外部サービスが必須（[06](06-voice-system.md)） |
| 思考（thinking） | **確認済**: 常に有効で無効化できない。深さは `effort` で調整。思考分も出力トークンとして課金 | 会話中の即時応答は遅延の実測が必要 |
| `temperature` 等 | **確認済**: 送ると 400 エラー | 「毎回違う会話」を乱数パラメータに頼れない。多様性はプログラム側の乱択で作る（[04](04-conversation-generation.md) §4） |
| 構造化出力 | **確認済**: 利用できる（CMEK 組織を除く） | 評価結果を決まった形式で受け取れる |
| 強制ツール呼び出し | **確認済**: `tool_choice` の `any` / `tool` は 400 エラー | 構造化出力を使う |
| 拒否（refusal） | **確認済**: 安全分類器が要求を断ることがあり、`stop_reason: "refusal"` で返る | 代替モデルへの切替設定と、題材の制限が必要（[04](04-conversation-generation.md) §8） |
| データ保持 | **確認済**: 30日保持が必須。ゼロデータ保持では使えない | プレイヤーの回答文が30日保持される旨の告知が必要 |
| 高速モード | **確認済**: $16 / $80 で利用できる | 即時応答の遅延対策の選択肢 |
| 応答の遅延 | **未検証** | 試作で実測する（[10](10-roadmap.md) 第2段階） |

比較対象の他モデル（確認済）: Opus 5.5 は $4 / $20、Sonnet 5.5 は $2 / $10、Haiku 5.5 は $0.10 / $0.50。

### 3.2 Unity

| 項目 | 結果 |
|---|---|
| 現行 LTS | **確認済**（unity.com）: Unity 6.3 LTS。サポートは 2027年12月まで |
| 最新版 | **第三者情報**（endoflife.date）: 6.6（2026-08-31 公開、最新パッチ 6000.6.8f1）。6.7 LTS が 2026年内の予定とされるが、公開日は確認できなかった |
| 料金 | **第三者情報＋unity.com**: Personal は年間収益・調達 20万ドルまで無料。Pro は 1席あたり年 $2,310。ランタイム料金は撤回済み |
| HDRP のモバイル対応 | **未検証**（従来は非対応。着手時に 6.3 の公式資料で再確認する） |
| WebGL でのマイク入力 | **未検証**。今回 WebGL は対象外とする |

### 3.3 音声サービス

料金はほぼすべて**第三者情報**で、記事ごとに数値が食い違う。日本語の品質を中立に比べた資料は見つからなかった。**したがって本設計では採用サービスを断定せず、検証手順と選定基準を定める**（[06](06-voice-system.md) §3、§4）。

| サービス | 分かったこと（第三者情報） |
|---|---|
| ElevenLabs Scribe v2 Realtime（音声認識） | 1時間 $0.39 前後。日本語対応。日本語精度は競合他社の主張しかない |
| OpenAI gpt-4o-transcribe（音声認識） | 1分 $0.006 前後 |
| Deepgram（音声認識） | 1分 $0.0043（一括処理）。ストリーミング対応 |
| ElevenLabs（音声合成） | v3 は 1,000文字 $0.10、Flash v2.5 は $0.05。日本語のアクセント品質は情報なし |
| Azure AI Speech（音声合成） | 標準ニューラル音声は 100万文字 $15〜16、HD は $22〜30（記事により差） |
| VOICEVOX（音声合成） | 無償・商用可・クレジット表記が条件 |
| whisper.unity（端末内の音声認識） | iOS・Android 対応、WebGL 非対応（確認済: 公開リポジトリの説明） |

### 3.4 公式 C# SDK

Anthropic 公式の C# SDK（NuGet `Anthropic`）は存在する（確認済）。**Unity（IL2CPP）上で動くかは未検証**。本設計では API キー保護のため Unity から Claude を直接呼ばず、中継サーバー経由とするので、この未検証点は設計を妨げない（[07](07-unity-architecture.md) §5）。

## 4. 用語（名称はすべて仮称）

| 用語 | 意味 |
|---|---|
| 聴き継ぎ（ききつぎ） | プレイヤーの役目。人の言葉を聞き、歪めずに運ぶ者 |
| 試練 | 会話1回と、その回答までのひとまとまり |
| 道行き（みちゆき） | 結び灯籠から次の結び灯籠までの、試練の連なり |
| 結び灯籠 | 再開地点 |
| 灯火（ともしび） | 道行きごとの残り回数。初期値3 |
| 申し送り | 聞いた内容を受け手に伝える回答 |
| 確度の印 | 申し送りの各文に付ける印。「聞いた」「推し量り」「不明」の3種 |
| 問い返し | 受け手からの質問。出題前に決めた固定の質問一式 |
| 成就／未達／歪み | 試練の結果。成功／罰なしの不成立／致命的失敗 |
| 物言い | 判定への異議申し立て |
| 情報素片 | 会話に含まれる情報の最小単位。評価の単位 |
| 評価基準票 | 情報素片ごとの重要度と正解を、出題前に確定させた表 |
| 構成役／検品役／会話役／評価役 | AI の役割。それぞれ別の指示・別の文脈で動く |
| 稽古／本番 | 罰なしの練習／罰ありの試練 |

## 5. 承認前に決めていただきたいこと

仕様に書かれておらず、推測で確定すべきでないと判断した点である。各項目には設計上の仮置きを示したが、**承認されるまで確定とは扱わない**。

| No. | 論点 | 仮置き | 影響 |
|---|---|---|---|
| Q1 | 個人利用か、販売する製品か | 未定のまま両対応の設計 | 料金、サーバー、認証、法務のすべて |
| Q2 | 「会話は毎回生成」の解釈。プレイヤーごとに毎回その場で生成するか、事前生成した使い捨てシナリオの在庫から「そのプレイヤーにとって初見」のものを出すことを認めるか | 在庫方式と都度生成の併用を推奨 | 1試練あたりの料金が数倍〜十数倍変わる |
| Q3 | 「不明点の正直な申告は罰なし」と「重要条件の完全な聞き漏らしは致命的」の両立 | 自覚して申告した欠落は罰なしだが成功にもならない（未達）。自覚のない欠落だけを致命的とする（[03](03-penalty-system.md) §3） | 罰システムの根幹 |
| Q4 | 明言されていない情報を事実として述べた場合、内容の軽重を問わず致命的か | 日時・人物・対象・条件・数量・場所・行動に関わる場合は致命的、周辺的な脚色は中程度 | 理不尽さの度合い |
| Q5 | 会話中のメモを認めるか | 既定は不可。会話後の整理の場面で書き出す | 記憶力ゲームになる度合い |
| Q6 | 対応機種の優先順位 | PC（Windows・macOS）を先行、スマートフォンは後続 | 描画品質、工程 |
| Q7 | 対応言語 | 日本語のみ | 音声サービスの選定 |
| Q8 | 字幕の扱い | 既定は非表示。設定で表示でき、文字で受け取る方式も用意する | 聴覚に頼れない人への配慮、記録の区分 |
| Q9 | 開発体制と予算（3Dアセットを誰が作るか） | 不明 | 工程と画質の現実性（[13](13-quality-audit.md) 指摘3） |
| Q10 | 対象年齢 | 不明 | 音声データの扱い、法務 |

## 6. 出典

- [Unity 6.3 LTS リリース（unity.com）](https://unity.com/ja/releases/unity-6)
- [Unity のサポート期間（unity.com）](https://unity.com/releases/unity-6/support)
- [Unity のバージョン一覧（endoflife.date）](https://endoflife.date/unity)
- [Unity の料金改定（unity.com）](https://unity.com/en/products/pricing-updates)
- [Messages API リファレンス（platform.claude.com）](https://platform.claude.com/docs/en/api/messages.md)
- [API リリースノート（platform.claude.com）](https://platform.claude.com/docs/en/release-notes/api)
- [拒否と代替モデル（platform.claude.com）](https://platform.claude.com/docs/en/build-with-claude/refusals-and-fallback)
- [ElevenLabs リアルタイム音声認識](https://elevenlabs.io/realtime-speech-to-text)
- [Scribe v2 Realtime の料金（Vercel AI Gateway）](https://vercel.com/ai-gateway/models/scribe-v2-realtime)
- [Soniox と ElevenLabs の日本語比較（競合他社による）](https://soniox.com/compare-stt/soniox-vs-elevenlabs/japanese)
- [音声認識 API の比較（Hakky）](https://book.st-hakky.com/hakky/voice-recognition-api-comparison-google-whisper-pricing-accuracy)
- [Azure AI Speech の料金（azure.microsoft.com）](https://azure.microsoft.com/en-us/pricing/details/speech/)
- [ElevenLabs の料金解説（activepieces）](https://www.activepieces.com/blog/elevenlabs-pricing-2026-plans-credits-and-costs.md)
- [音声合成 API の比較（Deepgram）](https://deepgram.com/blog/best-text-to-speech-apis-2026)
- [音声合成 API の比較（Fish Audio）](https://fish.audio/blog/text-to-speech-api-comparison-pricing-features/)
- [日本語音声合成の比較（crystal-method）](https://crystal-method.com/blog/voice-generation-ai-comparison/)
- [whisper.unity（GitHub）](https://www.github.com/Macoron/whisper.unity)
