# RICOH Live Streaming Client SDK for Windows Unity

Live Streaming API に接続するWindows Unityアプリケーション

## 動かし方

1. Unity Hubでunity_appをリストに追加し起動する
2. Projectの `Assets > Senes` で動作させたいSceneをダブルクリックする
3. Client ID, Secret, Room ID を取得する
4. 設定ファイルを作成する。
5. `File > Build And Run` を選択し、exeファイルを作成する

## シーン

* Watch
  * 映像・音声の受信のみを行う
* SampleScene
  * 映像・音声の双方向送受信を行う
* UnityCamera
  * Unityカメラの映像の送信、映像の受信、音声の双方向受信を行う
* ByteArraySender
  * 合成映像フレームの双方向送受信を行う

### 設定ファイル

* `unity_app/Secrets.template.json` を `unity_app/Assets/StreamingAssets/` にコピーし、`Secrets.json` に名前を変更する。
* `Secrets.json` を編集する。
    * `client_id`、 `client_secret`、`room_id` は実際の値を入力する。
```
{
    "client_id" : "",
    "client_secret" : "",
    "room_id" : "xxxxxx",
    "video_bitrate" : 20000
}
```

## ログ出力機能

`RTCStats` の通知イベントを受け取って ディスク上に書き込む機能がある。

`C:/Users/{ユーザー名}/AppData/LocalLow/RICOH/ClientSDKForWindows-UnitySample/logs/20200319T1336.log`という名前で出力される。
ファイル名は実際の日時で `yyyyMMdd'T'HHmm` の形式となる。
接続する度に新しいファイルが生成される。

ファイル形式は [LTSV](http://ltsv.org/) となっている。

すべての情報を出力しているのではなく `candidate-pair`, `stream`, `track`, `media-source`, `inbound-rtp`, `outbound-rtp`, `remote-inbound-rtp` の情報だけ出力している。

その他の情報を出力したい場合は `RTCStatsLogger.cs` を修正する。
出力可能な情報の一覧は https://www.w3.org/TR/webrtc-stats/ で確認できるが、
libwebrtc の実装に依存するため、記載されているすべての情報が出力できるとは限らない。

## 対応コーデック

現状対応しているコーデックは以下の通り。
- VP8、VP9ソフトウェアエンコーダ、デコーダ
- [NVIDIA VIDEO CODEC SDK](https://developer.nvidia.com/nvidia-video-codec-sdk)のH264ハードウェアエンコード、デコード

ソフトウェアでのH.264エンコード/デコードは未対応。

## 対応Unityバージョン
- Unity 2019.2
- Unity 2019.3

## 対応プラットフォーム
- Windows10 1903 x86_64以降

## 制限事項
- `DeviceUtil`クラスのメソッド呼び出しは、`Client`クラスのインスタンスが生成済みの状態で行う必要がある。
