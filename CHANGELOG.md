# CHANGELOG

## v2.1.0
- API 変更
  - [libwebrtc のログをファイルに出力するメソッド SetLibWebrtcLogOption を追加](https://api.livestreaming.ricoh/docs/clientsdk-api-external-specification/#setlibwebrtclogoption)しました

- SDK 修正
  - [NetworkError の追加と廃止](https://api.livestreaming.ricoh/docs/clientsdk-error-specification/#networkerror)を行いました
    - 追加: Room 管理 API (3 月までにリリース予定) でアプリケーションから切断された場合のエラーコード 53002 ConnectionClosedByApplication
    - 追加: Room の最大持続時間(24 時間)を越えて切断された場合のエラーコード 53003 MaxRoomPeriodExceeded
    - 廃止: 54000 OnTrackTimeout。同等の内容はログに出力されるのみになります

- サンプルアプリ修正
  - SetLibWebrtcLogOption の使用例を追加しました

## v2.0.0
* API変更
  * 破壊的変更 IClientListenerが実装を要求するコールバックメソッドの引数を1つに統一
    * 変更に伴う[移行ガイド](Guide.md#v200)を追加
    * 今後コールバックが渡す情報が追加される場合、引数のオブジェクトの属性が増えることになる
  * OnOpenのコールバック属性にAccessTokenのclaimのJSON文字列を追加

* SDK修正
  * libwebrtcをm102に更新
  * videoのmuteTypeがhardmuteで指定された際、Roomの帯域幅割当消費の対象外になった
  * `Client#ChangeMediaRequirements`を実行時に指定したConnectionが同じタイミングで離脱するとエラーになる場合がある問題を修正
  * OnOpenの直後やリスナー中でいくつかのメソッドを呼ぶと正常に動作しなかった問題を修正
  * OnOpen発生前にdisconnectを呼び出すとエラーになる問題の修正
  * Connect 中かつ Connecting 遷移前に Disconnect した場合に Closing および Closed 状態へ遷移しない問題の修正
  * OnError(WebSocketError) 発生時に Disconnectせずアプリケーションの判断に任せるように修正
  * 同じRoomに同じConnectionIDのConnectionが入室した際に、新しいConnectionが残り元のConnectionは退室する仕様変更に伴い、エラーコードを追加削除
    * 追加: 43603 SameConnectionIDJoined
    * 廃止: 43602 DuplicateConnectionIDOnConnect
  * 一部エラー内容の表現を改善
  * 下記ライブラリのバージョンアップ
    * log4net : 2.0.8 → 2.0.15
    * Newtonsoft.Json : 12.0.3 → 13.0.1

* サンプルアプリ修正
  * IClientListener の変更対応
  * 下記ライブラリのバージョンアップ
    * JWT : 7.2.1 → 9.0.3
  * サンプルアプリ間接続の簡単のためroom_id, room_specを揃えた

## v1.4.0

* API変更
  * 送信映像フレームレートを送信中に変更できる [Client#ChangeVideoSendFramerate(double maxFramerate)](https://api.livestreaming.ricoh/document/ricoh-live-streaming-client-sdk-api-%E5%A4%96%E9%83%A8%E4%BB%95%E6%A7%98/#changeVideoSendFramerate) を追加
  * (2022/06/29より本番環境で利用可能になりました)ConnectOptionのsending.enabledでクライアントの送信機能を無効にできるようにした。同一Room中に大量に送信機能が有効なクライアントが存在する場合、クライアントに大きな処理負荷や遅延が発生してしまうが、このオプションで低減することができる

* SDK修正
  * UnsupportedRoomSpecTypeOnChangeVideoSendBitrate のエラーコードを修正
    * 誤 45014 => 正 45018

* サンプルアプリ修正
  * Video Send Framerate 変更用のUIを追加（SampleScene）

## v1.3.0
* API変更
  * changeVideoSendBitrateで接続中に映像送信ビットレートを変更できるようにした
  * (dev環境のみ提供のβ機能)ConnectOptionのsending.enabledでクライアントの送信機能を無効にできるようにした。同一Room中に大量に送信機能が有効なクライアントが存在する場合、クライアントに大きな処理負荷や遅延が発生してしまうが、このオプションで低減することができる

* SDK修正
  * Connect と同時に Audio の Softmute を行った場合、Unmute 後に音声が出ない問題を修正
  * IceConnectionTimeoutエラーを通知
  * closing/closed状態で発生するInternalErrorをListenerへ通知しないように修正

* サンプルアプリ修正
  * Watch を受信専用モードに変更
  * SampleScene で UI から下記が行えるように変更
    * 送信ビットレート指定/変更
    * VideoCodec (H264/VP8/VP9)
    * Send/Receive (送受信/送信のみ/受信のみ)
    * Connection Metadata 実施
    * Audio/Video Track Metadata 実施

## v1.2.0
* API変更
  * ChangeMediaRequirementsで対向connectionごとにvideoを受信するか指定できるようにした  
  * OptionのIceServersProtocolでTURNサーバに接続する際にどのトランスポートプロトコルで接続するか指定できるようにした。この属性で"tls"を指定してTCP443ポートの強制が可能になり、他のトランスポートプロトコルを使ってパフォーマンス最適化する余地を犠牲にして、ファイヤーウォールやプロキシを通過する確率を上げることができる

* SDK修正
  * 特定のタイミングでchangeMute、updateTrackMetaを実行した場合に対向connectionに内容が通知されない不具合を修正
  * 要求されたRoomSpecに対応するSFUまたはTURNが一時的にクラウド上に存在しない場合に専用のエラーコード53806を追加
  * 依存するlibwebrtcのバージョンを m89.4389.5.5 から m93.4577.8 に変更
  * LAN ケーブルを抜いた状態で Disconnect を実施するとフリーズする問題の修正
  * Option#SetMeta(Dictionary<string, object> meta) の引数が null の場合、ArgumentNullException が発生することをAPI仕様書に明記

* サンプルアプリ修正
  * Video の選択受信用のチェックボックスを追加（SampleScene）
  * Connect で Exception が発生した際の回復処理を追加（SampleScene）
  * 5拠点目以降の映像が表示されない問題の修正（SampleScene, UnityCamera）
  * Update メソッドでの排他制御が原因でフリーズする問題の修正（SampleScene, UnityCamera, Watch）

## v1.1.4
* SDK修正
  * `SFU::PreAnswer` 対応
    * これに伴い以下のエラーコードを修正（エラーメッセージは変更なし）
      * 旧45207 => 新45605
      * 旧45216 => 新45614
  * Connect メッセージ の `SDKInfo` の `platform` を `windows` に変更
  * OnClosed 受信時に Closed 状態へ遷移せず Disconnect が完了しない問題の修正
  * P2P および P2PTurn にて接続時、AutioTrack と VideoTrack の MediaStream が異なる問題の修正
  * ネットワーク切断から一定時間（10数秒～60秒程度）経過後に `IClientListener#OnClosed` を通知するように修正。

* サンプルアプリ修正
  * Unityを LTS 2020.3.14f1 にバージョンアップ

## v1.1.0
* API変更
  * `MediaStreamTrack#SetEnabled(Boolean)` を public に変更
  * `ByteArrayFunction`、`ByteArrayCapturer`、`ByteArrayReceiver` をSDK外から不可視に変更

* SDK修正
  * サンプルアプリの CapabilityDropdown の表示内容が重複する問題を修正
  * `Client` クラスのインスタンスが生成前に `DeviceUtil` クラスのメソッド呼び出しを行った場合にアプリがクラッシュする問題を修正
  * P2P、P2PTurnで接続時に `Client#UpdateMeta` にて ConnectionMeta の更新が行えるように修正
    * これに伴い以下のエラーコードを削除
      * 45003

* サンプルアプリ修正
  * サンプルシーン `ByteArraySender` を削除

## v1.0.0
* API変更
  * `LSTrackOption#SetMeta` の引数 `metadata` が null の場合は ArgumentNullException を throw するように修正
  * webrtcのログの設定メソッドを `Client#SetConfigWebrtcLog` から `WebrtcLog#Create` に変更

* SDK修正
  * Track メタデータ未設定で Connect 後に `Client#ChangeMute` を呼び出した場合、ParameterError(45512) が発生する問題を修正
  * Disconnect後に `AudioTrack#SetVolume` を実行するとアプリがフリーズする問題を修正
  * Disconnect後に webrtc のログファイル出力が停止する問題を修正
  * libWebRTCのバージョンを m83.4103.12.1 から m89.4389.5.5 に変更
  * H.264でデコード時に RemoteTrack の画面が縦から横に回転されるとアスペクト比がおかしくなる問題を修正
  * H.264でデコード時に RemoteTrack の画面が横から縦に回転されるとアプリがクラッシュする問題を修正

* サンプルアプリ修正
  * SampleSceneでのremoteAudioTracksのクリア漏れ修正

## v1.0.0-alpha1
* API変更
  * LSTrack 名称変更
    * Track を LSTrack に変更
    * TrackOption を LSTrackOption に変更
    * Option#LocalTracks を Option#LocalLSTracks に変更
    * `Option#SetLocalTracks(List<Track> localTracks)` を `Option#SetLSLocalTracks(List<LSTrack> localLSTracks)` に変更
    * `Client#UpdateTrackMeta(Track track, Dictionary<string, object> meta)` を `Client#UpdateTrackMeta(LSTrack lsTrack, Dictionary<string, object> meta)` に変更
    * `Client#ReplaceMediaStreamTrack(Track targetTrack, MediaStreamTrack mediaStreamTrack)` を `Client#ReplaceMediaStreamTrack(LSTrack lsTrack, MediaStreamTrack mediaStreamTrack)` に変更
    * `Client#ChangeMute(Track targetTrack, MuteType nextMuteType)` を `Client#ChangeMute(LSTrack lsTrack, MuteType nextMuteType)` に変更
  * 下記をSDK外から不可視に変更  
    * com.ricoh.livestreaming
      * class `SourceTypeConverter`
      * enum `SourceType`
    * com.ricoh.livestreaming.unity
      * class `DeviceChangedNotifyThread`
      * class `INotifyThread`
    * com.ricoh.livestreaming.webrtc
      * method `MediaStream#AddTrack(AudioTrack)`
      * method `MediaStream#AddTrack(VideoTrack)`
      * method `MediaStream#RemoveTrack(AudioTrack)`
      * method `MediaStream#RemoveTrack(VideoTrack)`
      * method `MediaStreamTrack#SetEnabled(Boolean)`
      * class `PeerConnection`

* SDK修正
  * WebSocketでエラーが発生した場合にWebSocketErrorを通知するよう修正
  * Signalingエラーで正常終了時にも関わらずInternalErrorを通知していた問題を修正
  * SoftmuteとHardmuteの直接遷移に対応
  * Open状態以外で`Client#ChangeMute`を呼び出した場合のエラーコードを45005に変更
  * NVIDIAのGPU搭載PCで強制終了する問題を修正
  * P2Pで複数拠点と接続時にフリーズする問題を修正

* サンプルアプリ修正
  * bitrate_reservation_mbps の設定例を記載
  * MuteType(Softmute, Hardmute, Umnute) を Dropdown で切り替えられるように変更
  * RoomType(SFU, P2P, P2PTurn) を Dropdown で切り替えられるように変更

## v0.4.0
* API変更
  * IClientListener を変更
    * `IClientListener#OnError(string clientMessageType, string errorCode)` を `IClientListener#OnError(SDKErrorEvent errorEvent)` に変更
      * SDKErrorEvent#ToReportString()で詳細なエラー情報が取得可能
    * `IClientListener#OnRemoveRemoteConnection(string connectionId, Dictionary<string, object> metadata, List<MediaStreamTrack> mediaStreamTracks)` を `OnRemoveRemoteConnection(string connectionId, Dictionary<string, object> metadata, List<MediaStreamTrack> mediaStreamTracks)` に変更

* SDK修正
  * SignalingのURLを変更
  * Hardmuteの内部処理を修正
    * Hardmute時にもMediaStreamTrack.SetEnabled(false)を呼ぶよう修正

* サンプルアプリ修正
  * ドロップダウンリストの初期化方法を変更

## v0.3.0
* API変更
  * Client#GetStats()を追加
  * ConnectのOptionに IceTransportPolicy を追加
    * `Option#SetIceTransportPolicy(IceTransportPolicy iceTransportPolicy)` を追加
  * IClientListener を変更
    * `IClientListener#OnAddRemoteTrack(string connectionId, MediaStream stream, MediaStreamTrack track, Dictionary<string, object> metadata, MuteType muteType)` を追加
    * `IClientListener#OnUpdateRemoteTrack(string connectionId, MediaStream stream, MediaStreamTrack track, Dictionary<string, object> metadata)` を追加
    * `IClientListener#OnUpdateMute(string connectionId, MediaStream stream, MediaStreamTrack track, MuteType muteType)` を追加
    * `IClientListener#OnChangeStability(string connectionId, Stability stability)` を追加
  * DMCに対応
    * `SendingVideoOption`、`SendingOption`、 `ReceivingOption` を追加
    * `Video` を削除
      * `Codec`、 `Bitrate` の設定は `SendingVideoOption` で行うよう変更
    * `Audio` を削除
    * `Role` を削除
      * `Role` の設定は `ReceivingOption` で行うよう変更

* サンプルアプリ修正
  * コーデックの指定時の型を Video.Codec から SendingVideoOption.VideoCodecType に変更

`Codec`、 `Bitrate`設定の修正前の例:

```C#
var option = new Option()
    .SetVideo(new Video(Video.Codec.H264, VideoBitrate))
    .SetAudio(audioOption);
```

`Codec`、 `Bitrate`設定の修正後の例:

```C#
var sendingVideoOption = new SendingVideoOption()
    .SetCodec(SendingVideoOption.VideoCodecType.H264)
    .SetMaxBitrateKbps(VideoBitrate);

var option = new Option()
    .SetSendingOption(new SendingOption(sendingVideoOption));
```

`Role`設定の修正前の例:

```C#
var option = new Option()
    .SetRole(Role.SendOnly);
```

`Role`設定の修正後の例:

```C#
var option = new Option()
    .SetReceivingOption(new ReceivingOption(false));
```

## v0.2.1
* サンプルアプリ修正
  * JwtAccessTokenに設定するnbf、expの値を修正

## v0.2.0
* API変更
  * ミュート機能に対応
    * `Client#ChangeMute(Track targetTrack, MuteType nextMuteType)` を追加
  * ConnectのOptionに Role を追加
    * `Option#SetRole(Role role)` を追加
  * メタデータの更新通知に対応
    * `Client#UpdateMeta(ReadOnlyDictionary<string, object> meta)` を追加
  * IClientListener を変更
    * `IClientListener#OnUpdateRemoteConnection(string connectionId, Dictionary<string, object> metadata)` を追加
  * TrackOption、Role クラスを追加
  * Track のコンストラクタを変更
    * `Track(MediaStreamTrack track, MediaStream stream, Dictionary<string, object> metadata)` を `Track(MediaStreamTrack track, MediaStream stream, TrackOption option)` に変更
  * ReplaceTrackに対応
    * `Client#ReplaceMediaStreamTrack` を追加

* SDK修正
  * SignalingのURLを変更

* サンプルアプリ修正
  * ByteArraySender、SampleScene、UnityCameraでデバイスのミュートに対応
  * JwtAccessTokenに設定するnbf、expの値を修正
  * RoomSpecからUseTurnを削除
  * RoomSpecのRoomTypeにP2pTurnを追加

## v0.1.0
* 新APIに対応

## v0.0.5
* AudioTrack毎のボリューム変更に対応
  * ボリュームは`AudioTrack#SetVolume(volume)`で変更が可能
    * 設定可能範囲は0.0～10.0

* API変更
  * `RdcClient.Listener#OnAddRemoteTrack(MediaStreamTrack)` を `#OnAddRemoteTrack(MediaStream, MediaStreamTrack)` に変更
  
* サンプルアプリ修正
  * AudioTrack毎にボリュームが変更できるようサンプルアプリを修正

## v0.0.4
* NVIDIAハードウェアデコードに対応
  * H264コーデックに対応しているかどうかは`CodecUtil#IsH264Supported()`で確認が可能
  * コーデックの指定は`Configuration.Builder#Video(new Video(codec, bitrate))`で指定可能

* サンプルアプリ修正
  * H264が利用できる環境ではコーデックにH264を指定するよう修正
  * 設定値の保存タイミングを接続時からアプリ終了時に変更
  * 設定値の保存、読み込み処理を修正

## v0.0.3
Unityカメラ映像の送信に対応、合成映像フレームの送受信に対応

* API変更
  * `VideoDeviceCapturer`クラスを追加
    * カメラ映像の送信を行う場合に使用する
  * `UnityCameraCapturer` を追加
    * Unityカメラ映像の送信を行う場合に使用する
  * `ByteArrayFunction` を追加
    * 合成映像フレームの送受信を行う場合に使用する
  * `ByteArrayCapturer` を追加
    * 合成映像フレームの送信を行う場合に使用する
  * `ByteArrayReceiver` を追加
    * 合成映像フレームの受信を行う場合に使用する
  * `RdcClient#Connect(ticket, config, capturer)` を追加
    * `capturer`には以下のいずれかの指定が可能
      * 指定なし もしくは null : 映像の送信なし
      * VideoDeviceCapturer : カメラ映像の送信
      * UnityCameraCapturer : Unityカメラからの映像の送信
      * ByteArrayCapturer : カラーバッファ、深度バッファを合成した画像の送信
* サンプルアプリ修正
  * Unityカメラ映像の送信向けのサンプルシーン `UnityCamera` を追加
  * 合成映像フレームの送受信向けのサンプルシーン `ByteArraySender` を追加
* その他
  * libWebRTCのバージョンを m80.3987.2.2 から m83.4103.12.1 に変更
    * Azure Kinect V2接続時にクラッシュする問題が解消
    * ただし、Azure Kinect V2のマイクは使用できない

## v0.0.2

* サンプルアプリのStatsログの時間情報をUTC時間からローカル時間に変更
* サンプルアプリのStatsログのフィルタを変更

## v0.0.1

* 初版
  * VP8/VP9はソフトウェアエンコーダ/デコーダのみ対応
  * H264はNVIDIAハードウェアエンコーダのみ対応
