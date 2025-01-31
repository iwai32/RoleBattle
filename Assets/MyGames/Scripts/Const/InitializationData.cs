/// <summary>
/// 定数クラス 初期化時のデータ
/// </summary>
public static class InitializationData
{
    /// <summary>
    /// 最初のラウンド数
    /// </summary>
    public static readonly int INITIAL_ROUND_COUNT = 1;

    /// <summary>
    /// 獲得ポイント
    /// </summary>
    public static readonly int INITIAL_EARNED_POINT = 1;

    /// <summary>
    /// 最初の取得ポイント数
    /// </summary>
    public static readonly int INITIAL_POINT = 0;

    /// <summary>
    /// デフォルトのカウントダウンの秒数
    /// </summary>
    public static readonly int DEFAULT_COUNT_DOWN_TIME = 10;

    /// <summary>
    /// 必殺技発動によって獲得できるボーナスの倍率、獲得ポイントに掛けて使用する
    /// </summary>
    public static readonly int SPECIAL_SKILL_MAGNIFICATION_BONUS = 2;

    /// <summary>
    /// キャラクター未選択プレイヤーは初期表示用キャラのidを使用する
    /// </summary>
    public static readonly int CHARACTER_ID_FOR_UNSELECTED_PLAYER = 1;

    /// <summary>
    /// ネーム未編集のプレイヤーは未設定だとわかる名前にする
    /// </summary>
    public static readonly string PLAYER_NAME_FOR_UNEDITED_PLAYER = "Unknown";
}
