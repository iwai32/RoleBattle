using System.Collections;
using Cysharp.Threading.Tasks;

public interface IBattleManager
{
    PlayerData Player
    {
        get;
    }
    PlayerData Enemy
    {
        get;
    }

    BattlePhase BattlePhase
    {
        get;
    }

    /// <summary>
    /// ゲームを開始する
    /// </summary>
    /// <param name="isFirstGame"></param>
    /// <returns></returns>
    UniTask StartGame(bool isFirstGame);

    /// <summary>
    /// ゲームを終了
    /// </summary>
    void EndGame();

    /// <summary>
    /// バトルの段階を切り替える
    /// </summary>
    /// <param name="phase"></param>
    void ChangeBattlePhase(BattlePhase phase);

    /// <summary>
    /// カウントダウン
    /// </summary>
    IEnumerator CountDown();

    /// <summary>
    /// 結果を反映します
    /// </summary>
    /// <param name="result"></param>
    UniTask ReflectTheResult(CardJudgement result);

    /// <summary>
    /// 必殺技を使用します
    /// </summary>
    void UsedSpecialSkill(bool isPlayer);

    /// <summary>
    /// 必殺技の演出中かフラグをセットする
    /// </summary>
    void SetIsDuringProductionOfSpecialSkill(bool isDuringProduction);
}