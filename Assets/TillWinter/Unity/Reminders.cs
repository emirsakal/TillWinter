using System;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#elif UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace TillWinter.Unity
{
    /// <summary>
    /// One local reminder, opt-in (Settings → Reminders, off by default): when the app goes to the background during
    /// a year with something working on its own, a notification is booked for the moment the offline cap runs out,
    /// and cancelled the moment the app returns. Nothing is scheduled without the setting, nothing is sent from a
    /// server, and a denied permission simply leaves the switch doing nothing. Copy comes from Strings like every
    /// other line the player reads.
    /// </summary>
    public static class Reminders
    {
        private const string ChannelId = "tillwinter.farm";
        private const int AndroidId = 1;
        private const string IosId = "tillwinter.farm.away";

        public static bool Supported => Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer;

        /// <summary>Asks the system for permission (Android 13+, iOS). Called from the settings switch, never on launch.</summary>
        public static void RequestPermission()
        {
#if UNITY_ANDROID
            if (!Supported) return;
            Initialize();
            if (AndroidNotificationCenter.UserPermissionToPost != PermissionStatus.Allowed)
                new PermissionRequest(); // the system dialog; its answer is read back through UserPermissionToPost
#elif UNITY_IOS
            if (!Supported) return;
            new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Sound, true);
#endif
        }

        /// <summary>Books the one reminder <paramref name="seconds"/> from now; earlier bookings are replaced.</summary>
        public static void Schedule(double seconds, string title, string body)
        {
            if (!Supported || seconds < 60) return;
            Cancel();
#if UNITY_ANDROID
            Initialize();
            if (AndroidNotificationCenter.UserPermissionToPost != PermissionStatus.Allowed) return;
            var n = new AndroidNotification(title, body, DateTime.Now.AddSeconds(seconds)) { ShouldAutoCancel = true };
            AndroidNotificationCenter.SendNotificationWithExplicitID(n, ChannelId, AndroidId);
#elif UNITY_IOS
            var n = new iOSNotification
            {
                Identifier = IosId,
                Title = title,
                Body = body,
                ShowInForeground = false,
                Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = TimeSpan.FromSeconds(seconds), Repeats = false },
            };
            iOSNotificationCenter.ScheduleNotification(n);
#endif
        }

        /// <summary>The player is back: whatever was booked is moot.</summary>
        public static void Cancel()
        {
            if (!Supported) return;
#if UNITY_ANDROID
            Initialize();
            AndroidNotificationCenter.CancelNotification(AndroidId);
#elif UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(IosId);
            iOSNotificationCenter.RemoveDeliveredNotification(IosId);
#endif
        }

#if UNITY_ANDROID
        private static bool _initialized;

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            AndroidNotificationCenter.Initialize();
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel(ChannelId, "Till Winter", "Farm reminders", Importance.Default));
        }
#endif
    }
}
