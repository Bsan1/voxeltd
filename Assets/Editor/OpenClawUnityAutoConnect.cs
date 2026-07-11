#if UNITY_EDITOR
using OpenClaw.Unity;
using OpenClaw.Unity.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Project-side guard for OpenClaw editor autoconnect.
/// The package bridge already owns the connection; this only nudges it after reloads
/// and retries states the package retry loop does not recover from by itself.
/// </summary>
[InitializeOnLoad]
internal static class OpenClawUnityAutoConnect
{
    private const double FirstAttemptDelaySeconds = 3d;
    private const double RetryDelaySeconds = 6d;

    private static double _nextAttemptAt;
    private static int _attemptCount;
    private static OpenClawConnectionManager.ConnectionState _lastObservedState;

    static OpenClawUnityAutoConnect()
    {
        _lastObservedState = OpenClawConnectionManager.ConnectionState.Disconnected;
        ScheduleRetry(FirstAttemptDelaySeconds);

        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Window/OpenClaw Plugin/Auto Connect Now", false, 2)]
    private static void AutoConnectNow()
    {
        _attemptCount = 0;
        ScheduleRetry(0d);
        TryConnect("manual request");
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
        {
            _attemptCount = 0;
            ScheduleRetry(1d);
        }
    }

    private static void OnEditorUpdate()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            ScheduleRetry(FirstAttemptDelaySeconds);
            return;
        }

        if (EditorApplication.timeSinceStartup < _nextAttemptAt)
        {
            return;
        }

        var state = OpenClawEditorBridge.State;
        if (state != _lastObservedState)
        {
            _lastObservedState = state;
            if (state == OpenClawConnectionManager.ConnectionState.Disconnected ||
                state == OpenClawConnectionManager.ConnectionState.Error)
            {
                _attemptCount = 0;
            }
        }

        if (state == OpenClawConnectionManager.ConnectionState.Connected ||
            state == OpenClawConnectionManager.ConnectionState.Connecting ||
            state == OpenClawConnectionManager.ConnectionState.Reconnecting)
        {
            ScheduleRetry(RetryDelaySeconds);
            return;
        }

        TryConnect("editor autoconnect");
    }

    private static void TryConnect(string reason)
    {
        if (!CanAutoConnect())
        {
            ScheduleRetry(RetryDelaySeconds);
            return;
        }

        _attemptCount++;

        try
        {
            if (_attemptCount == 1 || _attemptCount % 10 == 0)
            {
                Debug.Log($"[OpenClaw AutoConnect] Connecting ({reason})...");
            }

            OpenClawEditorBridge.Connect();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[OpenClaw AutoConnect] Connect request failed: {e.Message}");
        }
        finally
        {
            ScheduleRetry(RetryDelaySeconds);
        }
    }

    private static bool CanAutoConnect()
    {
        var config = OpenClawConfig.Instance;
        return config != null &&
               config.autoConnect &&
               !string.IsNullOrWhiteSpace(config.gatewayUrl);
    }

    private static void ScheduleRetry(double delaySeconds)
    {
        _nextAttemptAt = EditorApplication.timeSinceStartup + delaySeconds;
    }
}
#endif
