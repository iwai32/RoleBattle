using System.Collections;
using Cysharp.Threading.Tasks;

public interface IBattleDataManager
{
    PlayerData Player
    {
        get;
    }
    PlayerData Enemy
    {
        get;
    }

    int RoundCount
    {
        get;
    }

    BattlePhase BattlePhase
    {
        get;
    }

    /// <summary>
    /// プレイヤーデータの作成
    /// </summary>
    void CreatePlayerData();

    /// <summary>
    /// ルームのデータの初期化
    /// </summary>
    void InitRoomData();

    /// <summary>
    /// プレイヤーデータの初期化
    /// </summary>
    void InitPlayerData();

    /// <summary>
    /// プレイヤーのターンを取得する
    /// </summary>
    /// <param name="isPlayer"></param>
    /// <returns></returns>
    bool GetPlayerTurnFor(bool isPlayer);

    /// <summary>
    /// プレイヤーのターンが終了したかどうかを取得する
    /// </summary>
    /// <param name="isPlayer"></param>
    /// <returns></returns>
    bool GetPlayerTurnEndFor(bool isPlayer);

    /// <summary>
    /// プレイヤーの状態をリセットする
    /// </summary>
    void ResetPlayerState();

    /// <summary>
    /// プレイヤーのターンを設定する
    /// </summary>
    void SetIsPlayerTurnFor(bool isPlayer, bool isMyTurn);

    /// <summary>
    /// プレイヤーのターンが終了したかどうかを設定する
    /// </summary>
    void SetIsPlayerTurnEndFor(bool isPlayer, bool isMyTurnEnd);

    /// <summary>
    /// プレイヤーが必殺技が発動可能かどうかを取得する
    /// </summary>
    /// <param name="isPlayer"></param>
    /// <returns></returns>
    bool GetCanUseSpSkillFor(bool isPlayer);

    /// <summary>
    /// バトルの段階を設定します
    /// </summary>
    /// <param name="battlePhase"></param>
    void SetBattlePhase(BattlePhase battlePhase);

    /// <summary>
    /// プレイヤーがフィールドにカードを置いたかどうかを設定する
    /// </summary>
    /// <param name="isPlayer"></param>
    /// <returns></returns>
    void SetIsFieldCardPlacedFor(bool isPlayer, bool isFieldCardPlaced);

    /// <summary>
    /// プレイヤーがフィールドにカードを置いたかどうかを取得する
    /// </summary>
    /// <param name="isPlayer"></param>
    /// <returns></returns>
    bool GetIsFieldCardPlacedFor(bool isPlayer);
}