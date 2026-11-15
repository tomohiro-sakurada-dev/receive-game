using System.Collections.Generic;
using System.Text;

namespace Receive.Core
{
    /// <summary>申し送りの欄。どの会話でも同じ8欄を示す（欄の有無が出題の手がかりにならないように）。</summary>
    public enum Slot { Person = 0, Task = 1, Item = 2, Quantity = 3, Day = 4, Time = 5, Place = 6, Condition = 7 }

    public enum Importance { Minor = 0, Major = 1, Critical = 2 }

    /// <summary>話し手がその欄について何を言ったか。出題前に確定する。</summary>
    public enum KeyState
    {
        NotStated = 0, // 言われていない
        Stated = 1,    // はっきり言った
        Guess = 2,     // 話し手自身の推測
        Hearsay = 3,   // 伝聞
        Vague = 4      // 「いつもの所」など。聞き返せば分かる
    }

    /// <summary>確度の印。</summary>
    public enum Mark { Heard = 0, Inferred = 1, Unknown = 2, NotSaid = 3 }

    public enum BeatKind { Greeting, Info, Chatter, Closing, Afterthought }

    public enum TrapKind { TailCondition, Afterthought, Correction, Guess, Hearsay, Vague, Distractor }

    public sealed class Beat
    {
        public int Index;
        public BeatKind Kind;
        public string Text;
    }

    /// <summary>出題内容。プレイヤーに見せてよいものだけを持つ。正解・重要度は持たない。</summary>
    public sealed class ScenarioScript
    {
        public int Level;
        public string PersonaId;
        public string SpeakerName;
        public string ReceiverName;
        public string Scene;
        public int Patience;
        public List<Beat> Beats = new List<Beat>();
    }

    /// <summary>情報素片。評価の単位。</summary>
    public sealed class InfoUnit
    {
        public Slot Slot;
        public KeyState State;
        public Importance Importance;
        public string Value;      // 語彙の識別子。条件の欄では「場合」
        public string Value2;     // 条件の欄の「その時の扱い」
        public string Decoy;      // 訂正前の値（訂正の罠）
        public int Beat = -1;     // 最終的な値が語られる区切り
        public int DecoyBeat = -1;
        public bool Late;         // 話の終わり近く、または訂正で確定する情報
        public string VagueWord;  // 「いつもの所」など
        public string DistractorValue; // 無関係な話に出てくる同じ欄の語

        public bool HasValue { get { return State != KeyState.NotStated; } }
    }

    /// <summary>受け手の問い返し。出題前に固定する。</summary>
    public sealed class Probe
    {
        public Slot Slot;
        public string Value;
        public string Value2;
        public string Text;
    }

    /// <summary>評価データ。出題内容とは別に保持し、出題前に封印する。</summary>
    public sealed class AnswerKey
    {
        public List<InfoUnit> Units = new List<InfoUnit>();
        public List<Probe> Probes = new List<Probe>();
        public List<TrapKind> Traps = new List<TrapKind>();

        public InfoUnit Unit(Slot s) { return Units[(int)s]; }

        /// <summary>封印に使う正規の文字列。内容が1文字でも変われば変わる。</summary>
        public string Canonical()
        {
            var sb = new StringBuilder();
            foreach (var u in Units)
            {
                sb.Append((int)u.Slot).Append('|').Append((int)u.State).Append('|').Append((int)u.Importance).Append('|')
                  .Append(u.Value).Append('|').Append(u.Value2).Append('|').Append(u.Decoy).Append('|')
                  .Append(u.Beat).Append('|').Append(u.DecoyBeat).Append('|').Append(u.Late ? 1 : 0).Append('|')
                  .Append(u.VagueWord).Append('|').Append(u.DistractorValue).Append('\n');
            }
            foreach (var p in Probes)
                sb.Append("P|").Append((int)p.Slot).Append('|').Append(p.Value).Append('|').Append(p.Value2).Append('|').Append(p.Text).Append('\n');
            foreach (var t in Traps) sb.Append("T|").Append((int)t).Append('\n');
            return sb.ToString();
        }
    }

    public sealed class Scenario
    {
        public long Seed;
        public string Fingerprint;
        public string ContentKey;   // 相手・用向き・品の組。近い重複の検出に使う
        public ScenarioScript Script;
        public AnswerKey Key;
        public string Seal;         // 評価データの要約値。出題前に記録する
        public long SealedAtTicks;
    }
}
