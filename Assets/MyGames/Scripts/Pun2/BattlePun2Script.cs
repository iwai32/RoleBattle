using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Photon.Pun.UtilityScripts;
using Cysharp.Threading.Tasks;
using PhotonHashTable = ExitGames.Client.Photon.Hashtable;
using static InitializationData;
using static BattlePhase;
using static CardJudgement;
using static CardType;

public class BattlePun2Script : MonoBehaviourPunCallbacks, IPunTurnManagerCallbacks
{
    [SerializeField]
    MultiBattleManager _multiBattleManager;

    [SerializeField]
    byte _maxPlayers = 2;

    [SerializeField]
    [Header("ゲーム盤のCanvasを設定する")]
    GameObject _multiBattleCanvas;

    [SerializeField]
    MultiBattleUIManager _multiBattleUIManager;

    bool _decidedTurn;
    bool _switchedTurn;
    bool _isEnemyIconPlaced;//エネミーのアイコンが設置されているか
    GameObject _playerIcon;//todo あとでスクリプト名になる可能性あり
    Player _player;
    Player _enemy;
    Room _room;
    PunTurnManager _punTurnManager;
    PhotonView _photonView;
    BattlePhase _battlePhase;


    void Awake()
    {
        _punTurnManager = GetComponent<PunTurnManager>();
        _punTurnManager.TurnManagerListener = this;
        _photonView = GetComponent<PhotonView>();
    }

    void Start()
    {
        PhotonNetwork.ConnectUsingSettings();
    }

    [PunRPC]
    void RpcStartBattle()
    {
        SearchEnemy();
        StartBattle(true).Forget();
    }

    /// <summary>
    /// 対戦相手を探します
    /// </summary>
    void SearchEnemy()
    {
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (_player.UserId != player.UserId)
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
        _multiBattleUIManager.ShowPointBy(false, player.GetPoint());
        _multiBattleUIManager.SetSpButtonImageBy(false, player.GetCanUseSpSkill());
        _enemy = player;
    }

    /// <summary>
    /// バトルを開始する
    /// </summary>
    public async UniTask StartBattle(bool isFirstGame)
    {
        //1ラウンド目に行う処理
        if (isFirstGame)
        {
            InitRoomData();
            //_battleUIManager.InitUIData();//中身は必殺技のdescriptionsの文言を設定できる、一旦保留
            DecideTheTurn();
        }
        ResetPlayerState();
        //_multiBattleUIManager.HideUIAtStart();
        _multiBattleUIManager.ResetFieldCards();
        await _multiBattleUIManager.ShowRoundCountText(_room.GetRoundCount());
        _multiBattleUIManager.DistributeCards();
    }

    /// <summary>
    /// 自クライアントのルーム入室時
    /// </summary>
    public override void OnJoinedRoom()
    {
        InitPlayerIcon();
        _player = PhotonNetwork.LocalPlayer;
        _room = PhotonNetwork.CurrentRoom;
        InitPlayerData();

        if (PhotonNetwork.PlayerList.Length == _maxPlayers)
        {
            _photonView.RPC("RpcStartBattle", RpcTarget.All);
        }
    }

    /// <summary>
    /// 先攻、後攻のターンを決めます
    /// </summary>
    public void DecideTheTurn()
    {
        if (_player.IsMasterClient == false) return;

        //trueならmasterClientを先攻にする
        if (RandomBool()) PhotonNetwork.MasterClient.SetIsMyTurn(true);
        else PhotonNetwork.PlayerListOthers[0].SetIsMyTurn(true);

        _decidedTurn = true;
    }

    /// <summary>
    /// ターンを開始します
    /// </summary>
    void StartTurn()
    {
        if (_decidedTurn == false) return;
        _decidedTurn = false;

        if (PhotonNetwork.IsMasterClient)
        {
            _punTurnManager.BeginTurn();
        }
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
    /// プレイヤーのターンを開始します
    /// </summary>
    /// <param name="isPlayer"></param>
    /// <returns></returns>
    async UniTask PlayerTurn()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            _room.SetIntBattlePhase(SELECTION);//カード選択フェイズへ
        }

        await _multiBattleUIManager.ShowThePlayerTurnText(_player.GetIsMyTurn());
        StopAllCoroutines();//前のカウントダウンが走っている可能性があるため一度止めます
        StartCoroutine(CountDown());
    }

    [PunRPC]
    void RpcPlayerTurn()
    {
        PlayerTurn().Forget();
    }

    /// <summary>
    /// プレイヤーのカスタムプロパティが呼び出された時
    /// </summary>
    /// <param name="targetPlayer"></param>
    /// <param name="changedProps"></param>
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashTable changedProps)
    {
        //自身と相手のデータへそれぞれ紐付けする
        bool isPlayer = IsUpdatePlayer(targetPlayer);

        _multiBattleUIManager.ShowPointBy(isPlayer, targetPlayer.GetPoint());
        _multiBattleUIManager.SetSpButtonImageBy(isPlayer, targetPlayer.GetCanUseSpSkill());
        CheckEnemyIcon();
        CheckPlayerTurnEnd();
        StartTurn();
        ChangeTurn();
    }

    /// <summary>
    /// ターンを切り替えます
    /// </summary>
    void ChangeTurn()
    {
        if (_switchedTurn == false) return;
        _switchedTurn = false;

        _photonView.RPC("RpcPlayerTurn", RpcTarget.AllViaServer);
    }

    // <summary>
    /// プレイヤーのターンのフラグを切り替えます
    /// </summary>
    void SwitchPlayerTurnFlg()
    {
        if (PhotonNetwork.IsMasterClient == false) return;
         
        _switchedTurn = true;
        _player.SetIsMyTurn(!_player.GetIsMyTurn());
        _enemy.SetIsMyTurn(!_enemy.GetIsMyTurn());
    }

    /// <summary>
    /// 更新プレイヤーかどうかを取得する
    /// </summary>
    /// <param name="player"></param>
    /// <returns></returns>
    bool IsUpdatePlayer(Player targetPlayer)
    {
        return (_player.UserId == targetPlayer.UserId);
    }

    /// <summary>
    /// エネミーのアイコンを調べます
    /// </summary>
    void CheckEnemyIcon()
    {
        if (_isEnemyIconPlaced) return;

        foreach (GameObject go in GameObject.FindGameObjectsWithTag("PlayerIcon"))
        {
            if (go != _playerIcon)
            {
                _multiBattleUIManager.PlacePlayerIconBy(false, go);
                _isEnemyIconPlaced = true;
            }
        }
    }

    /// <summary>
    /// プレイヤーのターンの終了を確認します
    /// </summary>
    /// <param name="player"></param>
    void CheckPlayerTurnEnd()
    {
        if (_player.GetIsMyTurnEnd() == false) return;
        _player.SetIsMyTurnEnd(false);
        _punTurnManager.SendMove(null, true);
    }

    /// <summary>
    /// プレイヤーの状態をリセットする
    /// </summary>
    public void ResetPlayerState()
    {
        _player.SetIsMyTurnEnd(false);
        _player.SetIsUsingSpInRound(false);
        _player.SetIsFieldCardPlaced(false);
    }

    /// <summary>
    /// bool型をランダムに取得する
    /// </summary>
    /// <returns></returns>
    bool RandomBool()
    {
        return UnityEngine.Random.Range(0, 2) == 0;
    }

    /// <summary>
    /// プレイヤーの進行状況を判断します
    /// </summary>
    void JudgePlayerProgress()
    {
        _player.SetIsMyTurnEnd(false);

        //お互いにカードをフィールドに配置していたらバトルをします。
        bool isBattle = (_player.GetIsFieldCardPlaced()
            && _enemy.GetIsFieldCardPlaced());

        if (isBattle) JudgeTheCard().Forget();
        else ChangeTurn();
    }

    /// <summary>
    /// カードを判定する
    /// </summary>
    async UniTask JudgeTheCard()
    {
        _room.SetIntBattlePhase(JUDGEMENT);

        CardType playerCardType = (CardType)_player.GetIntBattleCardType();
        CardType enemyCardType = (CardType)_enemy.GetIntBattleCardType();
        //じゃんけんする
        CardJudgement result = JudgeCardResult(playerCardType, enemyCardType);

        //OPENのメッセージを出す
        await _multiBattleUIManager.AnnounceToOpenTheCard();

        //お互いのplayerのBattleCardTypeからバトルの判定をする
        //自身の結果によってUIの表示を変える
        //customPropertiesへpointを加算
        //カードを裏返す時に相手のカードをすり替える

        //次のラウンドへ masterのみ
    }

    /// <summary>
    /// カードの勝敗結果を取得する
    /// </summary>
    /// <param name="myCard"></param>
    /// <param name="enemyCard"></param>
    /// <returns></returns>
    CardJudgement JudgeCardResult(CardType playerCardType, CardType enemyCardType)
    {
        //じゃんけんによる勝敗の判定
        if (playerCardType == enemyCardType) return DRAW;
        if (playerCardType == PRINCESS && enemyCardType == BRAVE) return WIN;
        if (playerCardType == BRAVE && enemyCardType == DEVIL) return WIN;
        if (playerCardType == DEVIL && enemyCardType == PRINCESS) return WIN;
        return LOSE;
    }

    /// <summary>
    /// カウントダウン
    /// </summary>
    public IEnumerator CountDown()
    {
        while (_punTurnManager.RemainingSecondsInTurn > 0)
        {
            _multiBattleUIManager.ShowCountDownText((int)_punTurnManager.RemainingSecondsInTurn);

            //必殺技の演出中はカウントしない
            //if (_isDuringProductionOfSpecialSkill == false)
            //{
            //    //1秒毎に減らしていきます
            //    yield return new WaitForSeconds(1f);
            //    _countDownTime--;
            //    _battleUIManager.ShowCountDownText(_countDownTime);
            //}

            yield return null;
        }
    }

    /// <summary>
    /// お互いのプレイヤーのターンが終了した時に呼ばれます
    /// </summary>
    /// <param name="turn"></param>
    void IPunTurnManagerCallbacks.OnTurnCompleted(int turn)
    {
        Debug.Log("ターン完了");
        //JudgePlayerProgress();
    }

    /// <summary>
    /// プレイヤーが移動したときに呼び出されます
    /// </summary>
    /// <param name="player"></param>
    /// <param name="turn"></param>
    /// <param name="move"></param>
    void IPunTurnManagerCallbacks.OnPlayerMove(Player player, int turn, object move)
    {
        Debug.Log("プレイヤーが移動");
    }

    /// <summary>
    /// プレイヤーがターンを終了したときに呼ばれます
    /// </summary>
    /// <param name="player"></param>
    /// <param name="turn"></param>
    /// <param name="move"></param>
    void IPunTurnManagerCallbacks.OnPlayerFinished(Player player, int turn, object move)
    {
        Debug.Log("ターン終了");
        SwitchPlayerTurnFlg();
    }

    /// <summary>
    /// タイムアウトでターンが終了した時に呼ばれます
    /// </summary>
    /// <param name="turn"></param>
    void IPunTurnManagerCallbacks.OnTurnTimeEnds(int turn)
    {
        Debug.Log("カウント終了!");
    }

    /// <summary>
    /// 接続完了時
    /// </summary>
    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinRandomRoom();
    }

    /// <summary>
    /// 入室失敗時
    /// </summary>
    /// <param name="returnCode"></param>
    /// <param name="message"></param>
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        RoomOptions opt = new RoomOptions();
        opt.MaxPlayers = _maxPlayers;
        opt.PublishUserId = true;//お互いにuserIdを見えるようにする
        PhotonNetwork.CreateRoom(null, opt);
    }

    /// <summary>
    /// プレイヤーアイコンの初期設定
    /// </summary>
    void InitPlayerIcon()
    {
        _playerIcon = PhotonNetwork.Instantiate("PlayerIcon", Vector3.zero, Quaternion.identity);
        _multiBattleUIManager.PlacePlayerIconBy(true, _playerIcon);
    }

    /// <summary>
    /// プレイヤーのデータの初期化
    /// </summary>
    void InitPlayerData()
    {
        _player.SetPoint(INITIAL_POINT);
        _player.SetCanUseSpSkill(true);
    }

    /// <summary>
    /// ルームのデータの初期化
    /// </summary>
    void InitRoomData()
    {
        if (_player.IsMasterClient == false) return;
        _room.SetRoundCount(INITIAL_ROUND_COUNT);
        _room.SetIntBattlePhase(BattlePhase.NONE);
    }

    /// <summary>
    /// ルームのカスタムプロパティが呼び出された時
    /// </summary>
    /// <param name="propertiesThatChanged"></param>
    public override void OnRoomPropertiesUpdate(PhotonHashTable propertiesThatChanged)
    {
        Debug.Log("updateBattlePhase" + propertiesThatChanged["BattlePhase"]);
    }

    /// <summary>
    /// 自クライアントがルームから退室した時
    /// </summary>
    public override void OnLeftRoom()
    {

    }

    /// <summary>
    /// 他クライアントがルームに入室した時
    /// </summary>
    /// <param name="newPlayer"></param>
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        _enemy = newPlayer;
    }

    /// <summary>
    /// 他クライアントがルームから離れた時、もしくは非アクティブになった時
    /// </summary>
    /// <param name="otherPlayer"></param>
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {

    }
}