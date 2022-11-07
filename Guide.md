# 移行ガイド

## v2.0.0
* v2.0.0 より `IClientListener` で定義している各種イベントハンドラの引数が変更になりましたので、下記の通り移行をお願いします
  * `IClientListener#OnConnecting()`
    * 旧
      ```c#
      void OnConnecting() {}
      ```
    * 新
      ```c#
      void OnConnecting(LSConnectingEvent lSConnectingEvent) {}
      ```
  * `IClientListener#OnOpen()`
    * 旧
      ```c#
      void OnOpen() {}
      ```
    * 新
      ```c#
      void OnOpen(LSOpenEvent lSOpenEvent) {}
      ```
  * `IClientListener#OnClosing()`
    * 旧
      ```c#
      void OnClosing() {}
      ```
    * 新
      ```c#
      void OnClosing(LSClosingEvent lSClosingEvent) {}
      ```
  * `IClientListener#OnClosed()`
    * 旧
      ```c#
      void OnClosed() {}
      ```
    * 新
      ```c#
      void OnClosed(LSCloseEvent lSCloseEvent) {}
      ```
  * `IClientListener#OnAddLocalTrack()`
    * 旧
      ```c#
      void OnAddLocalTrack(MediaStreamTrack mediaStreamTrack, MediaStream stream)
      {
          var track = mediaStreamTrack;
          var mediaStream = stream;
      }
      ```
    * 新
      ```c#
      void OnAddLocalTrack(LSAddLocalTrackEvent lSAddLocalTrackEvent)
      {
          var track = lSAddLocalTrackEvent.MediaStreamTrack;
          var mediaStream = lSAddLocalTrackEvent.Stream;
      }
      ```
  * `IClientListener#OnAddRemoteConnection()`
    * 旧
      ```c#
      void OnAddRemoteConnection(string connectionId, Dictionary<string, object> metadata)
      {
          var connId = connectionId;
          var meta = metadata;
      }
      ```
    * 新
      ```c#
      void OnAddRemoteConnection(LSAddRemoteConnectionEvent lSAddRemoteConnectionEvent)
      {
          var connId = lSAddRemoteConnectionEvent.ConnectionId;
          var meta = lSAddRemoteConnectionEvent.Metadata;
      }
      ```
  * `IClientListener#OnRemoveRemoteConnection()`
    * 旧
      ```c#
      void OnRemoveRemoteConnection(string connectionId, Dictionary<string, object> metadata, List<MediaStreamTrack> mediaStreamTracks)
      {
          var connId = connectionId;
          var meta = metadata;
          var tracks = mediaStreamTracks;
      }
      ```
    * 新
      ```c#
      void OnRemoveRemoteConnection(LSRemoveRemoteConnectionEvent lSRemoveRemoteConnectionEvent)
      {
          var connId = lSRemoveRemoteConnectionEvent.ConnectionId;
          var meta = lSRemoveRemoteConnectionEvent.Metadata;
          var tracks = lSRemoveRemoteConnectionEvent.MediaStreamTracks;
      }
      ```
  * `IClientListener#OnAddRemoteTrack()`
    * 旧
      ```c#
      void OnAddRemoteTrack(string connectionId, MediaStream stream, MediaStreamTrack mediaStreamTrack, Dictionary<string, object> metadata, MuteType muteType)
      {
          var connId = connectionId;
          var mediaStream = stream;
          var track = mediaStreamTrack;
          var meta = metadata;
          var mute = muteType;
      }
      ```
    * 新
      ```c#
      void OnAddRemoteTrack(LSAddRemoteTrackEvent lSAddRemoteTrackEvent)
      {
          var connId = lSAddRemoteTrackEvent.ConnectionId;
          var mediaStream = lSAddRemoteTrackEvent.Stream;
          var track = lSAddRemoteTrackEvent.MediaStreamTrack;
          var meta = lSAddRemoteTrackEvent.Metadata;
          var mute = lSAddRemoteTrackEvent.MuteType;
      }
      ```
  * `IClientListener#OnUpdateRemoteConnection()`
    * 旧
      ```c#
      void OnUpdateRemoteConnection(string connectionId, Dictionary<string, object> metadata)
      {
          var connId = connectionId;
          var meta = metadata;
      }
      ```
    * 新
      ```c#
      void OnUpdateRemoteConnection(LSUpdateRemoteConnectionEvent lSUpdateRemoteConnectionEvent)
      {
          var connId = lSUpdateRemoteConnectionEvent.ConnectionId;
          var meta = lSUpdateRemoteConnectionEvent.Metadata;
      }
      ```
  * `IClientListener#OnUpdateRemoteTrack()`
    * 旧
      ```c#
      void OnUpdateRemoteTrack(string connectionId, MediaStream stream, MediaStreamTrack mediaStreamTrack, Dictionary<string, object> metadata)
      {
          var connId = connectionId;
          var mediaStream = stream;
          var track = mediaStreamTrack;
          var meta = metadata;
      }
      ```
    * 新
      ```c#
      void OnUpdateRemoteTrack(LSUpdateRemoteTrackEvent lSUpdateRemoteTrackEvent)
      {
          var connId = lSUpdateRemoteTrackEvent.ConnectionId;
          var mediaStream = lSUpdateRemoteTrackEvent.Stream;
          var track = lSUpdateRemoteTrackEvent.MediaStreamTrack;
          var meta = lSUpdateRemoteTrackEvent.Metadata;
      }
      ```
  * `IClientListener#OnUpdateMute()`
    * 旧
      ```c#
      void OnUpdateMute(string connectionId, MediaStream stream, MediaStreamTrack mediaStreamTrack, MuteType muteType)
      {
          var connId = connectionId;
          var mediaStream = stream;
          var track = mediaStreamTrack;
          var mute = muteType;
      }
      ```
    * 新
      ```c#
      void OnUpdateMute(LSUpdateMuteEvent lSUpdateMuteEvent)
      {
          var connId = lSUpdateMuteEvent.ConnectionId;
          var mediaStream = lSUpdateMuteEvent.Stream;
          var track = lSUpdateMuteEvent.MediaStreamTrack;
          var mute = lSUpdateMuteEvent.MuteType;
      }
      ```
  * `IClientListener#OnChangeStability()`
    * 旧
      ```c#
      void OnChangeStability(string connectionId, Stability stability)
      {
          var connId = connectionId;
          var state = stability;
      }
      ```
    * 新
      ```c#
      void OnChangeStability(LSChangeStabilityEvent lSChangeStabilityEvent)
      {
          var connId = lSChangeStabilityEvent.ConnectionId;
          var state = lSChangeStabilityEvent.Stability;
      }
      ```
