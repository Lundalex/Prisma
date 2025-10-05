using UnityEngine;

public class NotificationManager2 : MonoBehaviour
{
    [SerializeField] private bool allowNotifications = true;
    [SerializeField] private NotifationKeyPair[] notificationKeyPairs;

    public void OpenNotification(string name)
    {
        if (!allowNotifications) return;

        bool notificationFound = false;
        foreach (NotifationKeyPair notifationKeyPair in notificationKeyPairs)
        {
            if (notifationKeyPair.notification == null)
            {
                Debug.Log("Notification not set for notification key pair with name '" + name + "'");
                continue;
            }

            if (name == notifationKeyPair.name)
            {
                if (!notificationFound)
                {
                    notifationKeyPair.notification.OpenNotification();
                    notificationFound = true;
                    continue;
                }
                else // More than one notification has been found
                {
                    Debug.LogWarning("More than one notification key pair with the name '" + name + "' found. One of the notification key pair's name has to be changed");
                }
            }

            notifationKeyPair.notification.CloseNotification();
        }

        if (!notificationFound) Debug.LogWarning("No notification key pair found with the name '" + name + "' found");
    }

    public void CloseNotification(string name)
    {
        if (!allowNotifications) return;
        
        bool notificationFound = false;
        foreach (NotifationKeyPair notifationKeyPair in notificationKeyPairs)
        {
            if (notifationKeyPair.notification == null)
            {
                Debug.Log("Notification not set for notification key pair with name '" + name + "'");
                continue;
            }

            if (name == notifationKeyPair.name)
            {
                if (!notificationFound)
                {
                    notifationKeyPair.notification.CloseNotification();
                    notificationFound = true;
                    continue;
                }
                else // More than one notification has been found
                {
                    Debug.LogWarning("More than one notification key pair with the name '" + name + "' found. One of the notification key pair's name has to be changed");
                }
            }
        }
    }
}