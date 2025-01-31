using System;
using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Photon.Pun.UtilityScripts;
using Cysharp.Threading.Tasks;
using static InitializationData;
using static BattlePhase;
using static BattleResult;
using static CardJudgement;
using static CardType;
using static WaitTimes;

public class MultiBattleManager : MonoBehaviourPunCallbacks,
    IPunTurnManagerCallbacks,
    IBattleAdvanceable,
    ITurnAdvanceable,
    IJudgableTheCard,
    ICountDowner
{
    [SerializeField]
    [Header("最大ラウンド数")]
    int _maxRoundCount = 3;

    [SerializeField]
    [Header("カウントダウンの秒数を指定")]
    int _defaultCountDownTime = DEFAULT_COUNT_DOWN_TIME;

    [SerializeField]
    [Header("ゲーム盤のCanvasを設定する")]
    MultiBattleUIManager _multiBattleUIManager;

    int _countDownTime;
    bool _canChangeTurn;
    PunTurnManager _punTurnManager;
    PhotonView _photonView;
    IMultiBattleDataManager _multiBattleDataManager;

    public PhotonView PhotonView => _photonView;


    void Awake()
    {
        _punTurnManager = gameObject.AddComponent<PunTurnManager>();
        _punTurnManager.TurnManagerListener = this;
        _photonView = GetComponent<PhotonView>();
    }

    void Start()
    {
        _multiBattleDataManager = ServiceLocator.Resolve<IMultiBattleDataManager>();
    }

    /// <summary>
    /// ゲームを再開する
    /// </summary>
    public void OnClickToRetryBattle()
    {
        //二重送信防止
        if (_multiBattleDataManager.Player.GetIsRetryingBattle()) return;
        _multiBattleDataManager.InitPlayerData();
        _multiBattleDataManager.Player.SetIsRetryingBattle(true);
        Loading._instance.ToggleUI(true);
        _multiBattleUIManager.ToggleDisplayLeaveRoomButton(true);
    }

    /// <summary>
    /// バトルを再開する
    /// </summary>
    public void RetryBattle()
    {
        if (_multiBattleDataManager.IsMasterClient())
        {
            _multiBattleDataManager.InitRoomData();
        }

        _photonView.RPC("RpcStartBattle", RpcTarget.AllViaServer, true);
    }

    /// <summary>
    /// タイトルへ移動する
    /// </summary>
    public void OnClickToTitle()
    {
        //ルームを退室後、切断しタイトルへ移動します
        PhotonNetwork.LeaveRoom();
    }

    /// <summary>
    /// 次のラウンドへの確認をします
    /// </summary>
    public void CheckToNextRound(IMultiBattleDataManager dataM)
    {
        //お互いのカードの判定が終わったら次のラウンドへ
        if (dataM.IsMasterClient() == false) return;
        if (dataM.IsCardJudgedForEachPlayer() == false) return;

        dataM.SetIsCardJudged(dataM.GetPlayerBy(true), false);
        dataM.SetIsCardJudged(dataM.GetPlayerBy(false), false);
        NextRound();
    }

    /// <summary>
    /// プレイヤーのターンの終了を確認します
    /// </summary>
    /// <param name="player"></param>
    public void CheckPlayerTurnEnd(IMultiBattleDataManager dataM)
    {
        if (dataM.GetIsMyTurnEnd(dataM.GetPlayerBy(true)) == false) return;
        dataM.SetIsMyTurnEnd(dataM.GetPlayerBy(true), false);
        _punTurnManager.SendMove(null, true);
    }

    /// <summary>
    /// 再戦をするか確認します
    /// </summary>
    public void CheckRetryingBattle(IMultiBattleDataManager dataM)
    {
        if (dataM.IsMasterClient() == false) return;
        if (dataM.IsRetryingBattleForEachPlayer() == false) return;

        dataM.SetIsRetryingBattle(dataM.GetPlayerBy(true), false);
        dataM.SetIsRetryingBattle(dataM.GetPlayerBy(false), false);
        //状態をリセットし、ゲーム再開
        RetryBattle();
    }

    /// <summary>
    /// 必殺技が発動していることを確認します
    /// </summary>
    public void CheckActivatingSpSkill(IMultiBattleDataManager dataM)
    {
        if (dataM.Room.GetIsDuringDirecting() == false) return;
        if (dataM.IsMasterClient() == false) return;
        dataM.Room.SetIsDuringDirecting(false);

        //発動後カウントダウンをリセットします
        _photonView.RPC("RpcResetCountDown", RpcTarget.AllViaServer);
    }

    /// <summary>
    /// プレイヤーアイコンの初期設定
    /// </summary>
    public void InitPlayerIcon()
    {
        _multiBattleUIManager.InitPlayerCharacter(true, GameManager._instance.GetPlayerCharacter());
    }

    /// <summary>
    /// バトルの準備をします
    /// </summary>
    [PunRPC]
    void RpcPrepareBattle()
    {
        SearchEnemy();
        _multiBattleDataManager.InitRoomData();
        GameManager._instance.PlayBgm(BgmType.BATTLE);
        RpcStartBattle(true);
    }

    [PunRPC]
    void RpcStartBattle(bool isFirstBattle)
    {
        Loading._instance.ToggleUI(false);
        _multiBattleUIManager.ToggleDisplayLeaveRoomButton(false);
        StartBattle(isFirstBattle).Forget();
    }

    /// <summary>
    /// バトルを開始する
    /// </summary>
    public async UniTask StartBattle(bool isFirstBattle)
    {
        IMultiBattleDataManager dataM = _multiBattleDataManager;
        //1ラウンド目に行う処理
        if (isFirstBattle)
        {
            _multiBattleUIManager.InitSpSkillDescriptions();
            DecideTheTurn();
        }
        dataM.ResetPlayerState();
        _multiBattleUIManager.HideUIAtStart();
        _multiBattleUIManager.ResetFieldCards();
        await _multiBattleUIManager.ShowRoundCountText(dataM.Room.GetRoundCount(), _maxRoundCount);
        _multiBattleUIManager.DistributeCards();
        StartTurn();
    }

    /// <summary>
    /// 対戦相手を探します
    /// </summary>
    void SearchEnemy()
    {
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (_multiBattleDataManager.IsEnemy(player.UserId))
            {
                SetEnemyInfo(player);
                break;
            }
        }
    }

    /// <summary>
    /// 対戦相手の情報を設定します
    /// </summary>
    void SetEnemyInfo(Player player)
    {
        //UIに対戦相手のキャラidに紐づいたキャラデータを設定します
        _multiBattleUIManager.InitPlayerCharacter(
            false,
            GameManager._instance.FindCharacterById(player.GetIsSelectedCharacterId())
         );
        _multiBattleUIManager.ShowPointBy(false, player.GetPoint());
        _multiBattleUIManager.SetSpButtonImageBy(false, player.GetCanUseSpSkill());
        _multiBattleDataManager.SetEnemy(player);
    }

    /// <summary>
    /// 先攻、後攻のターンを決めます
    /// </summary>
    public void DecideTheTurn()
    {
        IMultiBattleDataManager dataM = _multiBattleDataManager;
        if (dataM.IsMasterClient() == false) return;

        //trueならmasterClientを先攻にする
        if (BooleanUtil.RandomBool()) dataM.SetIsMyTurn(dataM.GetMasterClient(), true);
        else dataM.SetIsMyTurn(dataM.GetOtherPlayer(), true);
    }

    /// <summary>
    /// ターンを開始します
    /// </summary>
    public void StartTurn()
    {
        if (_multiBattleDataManager.IsMasterClient())
        {
            _punTurnManager.BeginTurn();
        }
    }

    [PunRPC]
    void RpcPlayerTurn()
    {
        PlayerTurn().Forget();
    }

    /// <summary>
    /// プレイヤーのターンを開始します
    /// </summary>
    /// <param name="isPlayer"></param>
    /// <returns></returns>
    public async UniTask PlayerTurn()
    {
        IMultiBattleDataManager dataM = _multiBattleDataManager;
        if (dataM.IsMasterClient())
        {
            dataM.Room.SetIntBattlePhase(SELECTION);//カード選択フェイズへ
        }

        await _multiBattleUIManager.ShowThePlayerTurnText(dataM.GetIsMyTurn(dataM.GetPlayerBy(true)));
        StopAllCoroutines();//前のカウントダウンが走っている可能性があるため一度止めます
        StartCoroutine(CountDown());
    }

    /// <summary>
    /// ターンを切り替えます
    /// </summary>
    public void ChangeTurn()
    {
        if (_canChangeTurn == false) return;
        _canChangeTurn = false;

        _photonView.RPC("RpcPlayerTurn", RpcTarget.AllViaServer);
    }

    // <summary>
    /// プレイヤーのターンのフラグを切り替えます
    /// </summary>
    public void SwitchPlayerTurnFlg()
    {
        IMultiBattleDataManager dataM = _multiBattleDataManager;

        if (dataM.IsMasterClient() == false) return;
        //どちらかのプレイヤーがまだフィールドにカードを出していない時のみターンを切り替えます
        if (dataM.IsEachPlayerFieldCardPlaced() == false)
            _canChangeTurn = true;

        //わかりずらいですが、player、enemyのisMyTurnフラグを逆にしてセットしています
        dataM.SetIsMyTurn(dataM.GetPlayerBy(true), !dataM.GetIsMyTurn(dataM.GetPlayerBy(true)));
        dataM.SetIsMyTurn(dataM.GetPlayerBy(false), !dataM.GetIsMyTurn(dataM.GetPlayerBy(false)));
    }

    /// <summary>
    /// カウントダウンをリセットします
    /// </summary>
    [PunRPC]
    void RpcResetCountDown()
    {
        StopAllCoroutines();//前のカウントダウンが走っている可能性があるため一度止めます
        StartCoroutine(CountDown());
    }

    /// <summary>
    /// カウントダウン
    /// </summary>
    public IEnumerator CountDown()
    {
        _countDownTime = _defaultCountDownTime;
        while (_countDownTime > 0)
        {
            //1秒毎に減らしていきます
            yield return new WaitForSeconds(1f);
            _countDownTime--;
            _multiBattleUIManager.ShowCountDownText(_countDownTime);
            yield return null;
        }

        DoIfCountDownTimeOut();
    }

    /// <summary>
    /// カウントダウン終了時の処理
    /// </summary>
    public void DoIfCountDownTimeOut()
    {
        IMultiBattleDataManager dataM = _multiBattleDataManager;
        if (dataM.GetIsMyTurn(dataM.GetPlayerBy(true)) == false) return;
        if (dataM.Room.GetIntBattlePhase() != (int)SELECTION) return;

        //確認画面を全て閉じ、ランダムにカードを移動
        _multiBattleUIManager.InactiveUIIfCountDownTimeOut();
        _multiBattleUIManager.MoveRandomCardToField(true).Forget();
    }

    /// <summary>
    /// 次のラウンドへ進みます
    /// </summary>
    void NextRound()
    {
        if (_multiBattleDataManager.Room.GetRoundCount() != _maxRoundCount)
        {
            AddRoundCount();
            _photonView.RPC("RpcStartBattle", RpcTarget.AllViaServer, false);
        }
        else
        {
            _photonView.RPC("EndBattle", RpcTarget.AllViaServer);
        }
    }

    /// <summary>
    /// バトルを終了する
    /// </summary>
    [PunRPC]
    public void EndBattle()
    {
        //勝敗を表示
        _multiBattleUIManager.ShowBattleResultUI(true, CommonAttribute.GetStringValue(JudgeBattleResult()));
    }

    /// <summary>
    /// バトルの結果を取得する
    /// </summary>
    public BattleResult JudgeBattleResult()
    {
        IMultiBattleDataManager dataM = _multiBattleDataManager;
        int playerPoint = dataM.GetPoint(dataM.GetPlayerBy(true));
        int enemyPoint = dataM.GetPoint(dataM.GetPlayerBy(false));

        if (playerPoint > enemyPoint) return BATTLE_WIN;
        if (playerPoint == enemyPoint) return BATTLE_DRAW;
        return BATTLE_LOSE;
    }

    /// <summary>
    /// ラウンドの増加
    /// </summary>
    void AddRoundCount()
    {
        int totalRoundCount = _multiBattleDataManager.Room.GetRoundCount();
        totalRoundCount++;
        _multiBattleDataManager.Room.SetRoundCount(totalRoundCount);
    }

    /// <summary>
    /// プレイヤーの進行状況を判断します
    /// </summary>
    public void JudgePlayerProgress()
    {
        //お互いにカードをフィールドに配置していたらバトルをします。
        if (_multiBattleDataManager.IsEachPlayerFieldCardPlaced())
            JudgeTheCard().Forget();
        else
            Debug.Log("フィールドにカードを配置していないプレイヤーが存在します。");
    }

    /// <summary>
    /// 結果によるポイントを加算する
    /// </summary>
    void AddPointBy(CardJudgement result)
    {
        if (result != WIN) return;

        IMultiBattleDataManager dataM = _multiBattleDataManager;
        dataM.GetPoint(dataM.GetPlayerBy(true));

        int totalPoint = dataM.GetPoint(dataM.GetPlayerBy(true)) + EarnPoint(dataM.GetIsUsingSpInRound(dataM.GetPlayerBy(true)));
        dataM.SetPoint(dataM.GetPlayerBy(true), totalPoint);
    }

    /// <summary>
    /// 獲得ポイント
    /// </summary>
    /// <returns></returns>
    public int EarnPoint(bool isUsingSpSkillInRound)
    {
        int earnPoint = _multiBattleDataManager.Room.GetEarnedPoint();
        //このラウンドの間必殺技を使用していた場合
        if (isUsingSpSkillInRound)
            earnPoint *= SPECIAL_SKILL_MAGNIFICATION_BONUS;

        return earnPoint;
    }

    /// <summary>
    /// カードを判定する
    /// </summary>
    public async UniTask JudgeTheCard()
    {
        IMultiBattleDataManager dataM = _multiBattleDataManager;

        if (dataM.IsMasterClient())
            dataM.Room.SetIntBattlePhase(JUDGEMENT);

        CardType playerCardType = (CardType)dataM.GetIntBattleCardType(dataM.GetPlayerBy(true));
        CardType enemyCardType = (CardType)dataM.GetIntBattleCardType(dataM.GetPlayerBy(false));
        //じゃんけんする
        CardJudgement result = JudgeCardResult(playerCardType, enemyCardType);

        //OPENのメッセージを出す
        await _multiBattleUIManager.AnnounceToOpenTheCard();
        //相手のカードをenemyCardTypeに対応したカードにすり替える
        await _multiBattleUIManager.ReplaceEnemyFieldCard(enemyCardType);
        //カードを表にする
        await _multiBattleUIManager.OpenTheBattleFieldCards();
        //結果の反映
        await ReflectTheResult(result);
        //ポイントの追加
        AddPointBy(result);
        await UniTask.Delay(TimeSpan.FromSeconds(TIME_BEFORE_CHANGING_ROUND));
        //判定終了フラグをオンにする
        dataM.SetIsCardJudged(dataM.GetPlayerBy(true), true);
    }

    /// <summary>
    /// カードの勝敗結果を取得する
    /// </summary>
    /// <param name="myCard"></param>
    /// <param name="enemyCard"></param>
    /// <returns></returns>
    public CardJudgement JudgeCardResult(CardType playerCardType, CardType enemyCardType)
    {
        //じゃんけんによる勝敗の判定
        if (playerCardType == enemyCardType) return DRAW;
        if (playerCardType == PRINCESS && enemyCardType == BRAVE) return WIN;
        if (playerCardType == BRAVE && enemyCardType == DEVIL) return WIN;
        if (playerCardType == DEVIL && enemyCardType == PRINCESS) return WIN;
        return LOSE;
    }

    /// <summary>
    /// 結果を反映します
    /// </summary>
    /// <param name="result"></param>
    public async UniTask ReflectTheResult(CardJudgement result)
    {
        if (_multiBattleDataManager.IsMasterClient())
        {
            _multiBattleDataManager.Room.SetIntBattlePhase(RESULT);
        }
        await _multiBattleUIManager.ShowJudgementResultText(result.ToString());
    }

    /// <summary>
    /// ターン開始時に呼ばれます
    /// </summary>
    /// <param name="turn"></param>
    void IPunTurnManagerCallbacks.OnTurnBegins(int turn)
    {
        PlayerTurn().Forget();
    }

    /// <summary>
    /// お互いのプレイヤーのターンが終了した時に呼ばれます
    /// </summary>
    /// <param name="turn"></param>
    void IPunTurnManagerCallbacks.OnTurnCompleted(int turn)
    {
        StopAllCoroutines();//カウントダウンを止めます
        JudgePlayerProgress();
    }

    /// <summary>
    /// プレイヤーが移動したときに呼び出されます
    /// </summary>
    /// <param name="player"></param>
    /// <param name="turn"></param>
    /// <param name="move"></param>
    void IPunTurnManagerCallbacks.OnPlayerMove(Player player, int turn, object move)
    {
        //今回は使用しません
    }

    /// <summary>
    /// プレイヤーがターンを終了したときに呼ばれます
    /// </summary>
    /// <param name="player"></param>
    /// <param name="turn"></param>
    /// <param name="move"></param>
    void IPunTurnManagerCallbacks.OnPlayerFinished(Player player, int turn, object move)
    {
        SwitchPlayerTurnFlg();
    }

    /// <summary>
    /// タイムアウトでターンが終了した時に呼ばれます
    /// </summary>
    /// <param name="turn"></param>
    void IPunTurnManagerCallbacks.OnTurnTimeEnds(int turn)
    {
        //今回は使用しません
    }
}